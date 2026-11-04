using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.NotasFiscais;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.NotasFiscais;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Renave;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.NotasFiscais;
using ConnectVeiculos.Core.Renave;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.NotasFiscais
{
    /// <summary>
    /// NF-e de veiculos emitida direto na Sefaz. A configuracao da loja (emitente,
    /// serie, numeracao e regras fiscais) fica em um JSON na ConfiguracaoSistema.
    /// Autorizada a nota, a chave vai sozinha para o Renave quando o veiculo
    /// esta no estoque eletronico.
    /// </summary>
    public class NfeService : INfeService
    {
        private const string KeyConfig = "NFE_CONFIG";
        private static readonly TimeSpan PrazoCancelamento = TimeSpan.FromHours(24);
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        // Numeracao: uma reserva por vez em cada loja (tenant), senao duas notas
        // simultaneas pegariam o mesmo numero.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> TravasNumeracao = new();
        private static readonly ConcurrentDictionary<string, IReadOnlyList<NfeMunicipio>> MunicipiosPorUf = new();

        private readonly IConfiguracaoSistemaRepository _config;
        private readonly INotaFiscalRepository _notas;
        private readonly IVeiculoRepository _veiculos;
        private readonly IVendaRepository _vendas;
        private readonly IVeiculoRenaveRepository _registrosRenave;
        private readonly ICertificadoDigitalService _certificado;
        private readonly INfeSefaz _sefaz;
        private readonly IRenaveService _renave;
        private readonly ITenantContext _tenant;
        private readonly HttpClient _http;
        private readonly ILogger<NfeService> _logger;

        public NfeService(IConfiguracaoSistemaRepository config, INotaFiscalRepository notas, IVeiculoRepository veiculos,
            IVendaRepository vendas, IVeiculoRenaveRepository registrosRenave,
            ICertificadoDigitalService certificado, INfeSefaz sefaz, IRenaveService renave, ITenantContext tenant,
            HttpClient http, ILogger<NfeService> logger)
        {
            _config = config;
            _notas = notas;
            _veiculos = veiculos;
            _vendas = vendas;
            _registrosRenave = registrosRenave;
            _certificado = certificado;
            _sefaz = sefaz;
            _renave = renave;
            _tenant = tenant;
            _http = http;
            _logger = logger;
        }

        // ================================================================
        // Configuracao
        // ================================================================

        public async Task<NfeConfigInfo> ObterConfigAsync(CancellationToken ct = default)
        {
            var json = await _config.GetValorAsync(KeyConfig);
            NfeConfigInfo cfg;
            try { cfg = string.IsNullOrWhiteSpace(json) ? Padrao() : JsonSerializer.Deserialize<NfeConfigInfo>(json, Json) ?? Padrao(); }
            catch (JsonException) { cfg = Padrao(); }

            var cert = await _certificado.ObterInfoAsync(ct);
            cfg.CertificadoValido = cert.Valido;
            cfg.CertificadoCnpj = cert.Cnpj;
            cfg.Pendencias = Pendencias(cfg);
            cfg.Pronto = cfg.Pendencias.Count == 0;
            return cfg;
        }

        public async Task<NfeConfigInfo> SalvarConfigAsync(NfeConfigInfo input, CancellationToken ct = default)
        {
            var cnpj = NfeVeiculoBuilder.SoDigitos(input.Cnpj);
            if (cnpj.Length > 0 && cnpj.Length != 14) throw new NfeException("CNPJ do emitente inválido.");
            if (input.Crt is < 1 or > 4) throw new NfeException("Regime tributário inválido.");
            if (input.Serie is < 0 or > 999) throw new NfeException("Série deve ficar entre 0 e 999.");
            if (input.ProximoNumero is < 1 or > 999_999_999) throw new NfeException("Número da próxima nota inválido.");
            if (!string.IsNullOrWhiteSpace(input.Uf)) NfeVeiculoBuilder.Uf(input.Uf);

            input.Ambiente = input.Ambiente == "producao" ? "producao" : "homologacao";
            input.Cnpj = cnpj;
            // Campos calculados nao sao gravados.
            input.CertificadoValido = false;
            input.CertificadoCnpj = null;
            input.Pendencias = new();
            input.Pronto = false;

            await _config.SetValorAsync(KeyConfig, JsonSerializer.Serialize(input, Json));
            return await ObterConfigAsync(ct);
        }

        private static NfeConfigInfo Padrao() => new()
        {
            Compra = new NfeRegraOperacao
            {
                NaturezaOperacao = "Compra de veiculo usado", CfopInterno = "1102", CfopInterestadual = "2102",
                CstIcms = "900", CstPis = "49", CstCofins = "49"
            },
            Venda = new NfeRegraOperacao
            {
                NaturezaOperacao = "Venda de veiculo usado", CfopInterno = "5102", CfopInterestadual = "6102",
                CstIcms = "102", CstPis = "49", CstCofins = "49"
            }
        };

        private static List<string> Pendencias(NfeConfigInfo c)
        {
            var p = new List<string>();
            if (NfeVeiculoBuilder.SoDigitos(c.Cnpj).Length != 14) p.Add("CNPJ da loja");
            if (string.IsNullOrWhiteSpace(c.InscricaoEstadual)) p.Add("Inscrição estadual");
            if (string.IsNullOrWhiteSpace(c.RazaoSocial)) p.Add("Razão social");
            if (string.IsNullOrWhiteSpace(c.Logradouro) || string.IsNullOrWhiteSpace(c.Bairro) || string.IsNullOrWhiteSpace(c.Cep))
                p.Add("Endereço da loja");
            if (c.CodigoMunicipio is null or < 1000000 || string.IsNullOrWhiteSpace(c.Uf)) p.Add("Município da loja");
            if (!c.CertificadoValido) p.Add("Certificado A1 válido");
            else if (c.CertificadoCnpj != null && NfeVeiculoBuilder.SoDigitos(c.Cnpj) is { Length: 14 } cnpj
                     && !string.Equals(cnpj[..8], c.CertificadoCnpj[..8], StringComparison.Ordinal))
                p.Add("Certificado de outra empresa (CNPJ diferente do da loja)");
            return p;
        }

        public async Task<NfeStatusSefaz> ConsultarStatusSefazAsync(CancellationToken ct = default)
        {
            var cfg = await ObterConfigAsync(ct);
            using var cert = await CertificadoObrigatorioAsync(ct);
            try
            {
                return _sefaz.Status(NfeVeiculoBuilder.Uf(cfg.Uf), cfg.Ambiente != "producao", cert);
            }
            catch (Exception ex) when (ex is not NfeException)
            {
                _logger.LogWarning(ex, "NF-e: falha ao consultar o status da Sefaz");
                throw new NfeException("Não foi possível falar com a Sefaz: " + ex.Message);
            }
        }

        public async Task<IReadOnlyList<NfeMunicipio>> BuscarMunicipiosAsync(string uf, string? nome, CancellationToken ct = default)
        {
            var sigla = NfeVeiculoBuilder.Uf(uf).ToString();
            if (!MunicipiosPorUf.TryGetValue(sigla, out var lista))
            {
                try
                {
                    var resp = await _http.GetFromJsonAsyncCompat($"https://servicodados.ibge.gov.br/api/v1/localidades/estados/{sigla}/municipios", ct);
                    lista = resp.Select(m => new NfeMunicipio { Codigo = m.Id, Nome = m.Nome, Uf = sigla }).ToList();
                    MunicipiosPorUf[sigla] = lista;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
                {
                    _logger.LogWarning(ex, "NF-e: falha ao buscar municipios no IBGE");
                    throw new NfeException("Não foi possível buscar os municípios no IBGE agora.");
                }
            }

            if (string.IsNullOrWhiteSpace(nome)) return lista;
            var busca = NfeVeiculoBuilder.SemAcento(nome.Trim()).ToUpperInvariant();
            return lista.Where(m => NfeVeiculoBuilder.SemAcento(m.Nome).ToUpperInvariant().Contains(busca)).Take(20).ToList();
        }

        // ================================================================
        // Notas
        // ================================================================

        public async Task<IReadOnlyList<NfeResumo>> ListarDoVeiculoAsync(int veiculoId, CancellationToken ct = default)
            => (await _notas.GetByVeiculoAsync(veiculoId)).OrderByDescending(n => n.NfId).Select(Resumo).ToList();

        /// <summary>Quantas notas a tela Notas fiscais traz de uma vez (o filtro estreita).</summary>
        public const int LimiteLista = 500;

        /// <summary>Notas de todos os veiculos, para a tela Notas fiscais.</summary>
        public async Task<IReadOnlyList<NfeResumo>> ListarAsync(NfeFiltro filtro, CancellationToken ct = default)
        {
            if (filtro.De.HasValue && filtro.Ate.HasValue && filtro.De.Value.Date > filtro.Ate.Value.Date)
                throw new NfeException("A data inicial está depois da data final.");
            filtro.Tipo = string.IsNullOrWhiteSpace(filtro.Tipo) ? null : Tipo(filtro.Tipo);
            filtro.Status = string.IsNullOrWhiteSpace(filtro.Status) ? null : filtro.Status.Trim().ToUpperInvariant();

            return (await _notas.ListarAsync(filtro, LimiteLista)).Select(x =>
            {
                var resumo = Resumo(x.Nota);
                resumo.Veiculo = x.Marca == null ? null : $"{x.Marca} {x.Modelo} {x.Ano}".Trim();
                resumo.Placa = x.Placa;
                return resumo;
            }).ToList();
        }

        /// <summary>
        /// Preenche a nota com o que o sistema ja sabe, nesta ordem: a ultima nota
        /// do mesmo tipo do veiculo (rejeitada ou cancelada), a venda registrada
        /// (so na venda) e o Renave (vendedor na compra, comprador na venda).
        /// </summary>
        public async Task<NfeSugestao> SugerirAsync(int veiculoId, string tipo, CancellationToken ct = default)
        {
            tipo = Tipo(tipo);
            var veiculo = await _veiculos.GetByIdAsync(veiculoId) ?? throw new NfeException("Veículo não encontrado.");
            var cfg = await ObterConfigAsync(ct);
            var dados = new NfeEmissaoInput
            {
                Tipo = tipo,
                Endereco = new NfeEndereco { Uf = cfg.Uf },
                Valor = tipo == NotaFiscal.Saida ? veiculo.VeiPreco : veiculo.VeiPrecoCompra,
                MeioPagamento = tipo == NotaFiscal.Saida ? "17" : "90"
            };

            var anterior = (await _notas.GetByVeiculoAsync(veiculoId))
                .Where(n => n.NfTipo == tipo && n.NfDadosJson != null)
                .OrderByDescending(n => n.NfId)
                .FirstOrDefault();
            if (anterior != null)
            {
                try
                {
                    var salvo = JsonSerializer.Deserialize<NfeEmissaoInput>(anterior.NfDadosJson!, Json);
                    if (salvo != null)
                    {
                        salvo.Tipo = tipo;
                        salvo.Endereco ??= new NfeEndereco { Uf = cfg.Uf };
                        return new NfeSugestao { Dados = salvo, Origem = "nota" };
                    }
                }
                catch (JsonException) { /* segue para as outras fontes */ }
            }

            if (tipo == NotaFiscal.Saida)
            {
                var venda = (await _vendas.GetByVeiculoIdAsync(veiculoId))
                    .Where(v => v.VenStatus != "E")
                    .OrderByDescending(v => v.VenDtVenda)
                    .FirstOrDefault();
                if (venda != null)
                {
                    dados.Documento = NfeVeiculoBuilder.SoDigitos(venda.VenCompradorCpf);
                    dados.Nome = venda.VenCompradorNome?.Trim() ?? "";
                    dados.Email = Vazio(venda.VenCompradorEmail);
                    dados.Telefone = Vazio(venda.VenCompradorTelefone);
                    if (venda.VenValor > 0) dados.Valor = venda.VenValor;
                    dados.MeioPagamento = MeioPagamentoDaVenda(venda.VenFormaPagamento);
                    dados.Endereco = LerEnderecoDaVenda(venda.VenCompradorEndereco, cfg.Uf);
                    await CompletarMunicipioAsync(dados.Endereco, ct);
                    return new NfeSugestao { Dados = dados, Origem = "venda" };
                }
            }

            var renave = await _registrosRenave.GetByVeiculoAsync(veiculoId);
            if (tipo == NotaFiscal.Entrada && renave?.RnvVendedorDocumento != null)
            {
                dados.Documento = renave.RnvVendedorDocumento;
                dados.Email = renave.RnvVendedorEmail;
                if (renave.RnvValorCompra > 0) dados.Valor = renave.RnvValorCompra.Value;
                return new NfeSugestao { Dados = dados, Origem = "renave" };
            }
            if (tipo == NotaFiscal.Saida && renave?.RnvCompradorDocumento != null)
            {
                dados.Documento = renave.RnvCompradorDocumento;
                dados.Nome = renave.RnvCompradorNome ?? "";
                dados.Email = renave.RnvCompradorEmail;
                if (renave.RnvValorVenda > 0) dados.Valor = renave.RnvValorVenda.Value;
                return new NfeSugestao { Dados = dados, Origem = "renave" };
            }

            return new NfeSugestao { Dados = dados };
        }

        /// <summary>Forma de pagamento da venda (tela de vendas) para o codigo tPag da NF-e.</summary>
        public static string MeioPagamentoDaVenda(string? forma) => (forma ?? "").ToUpperInvariant() switch
        {
            "PIX" => "17",
            "DINHEIRO" => "01",
            "CARTAO_CREDITO" => "03",
            "CARTAO_DEBITO" => "04",
            _ => "99"
        };

        /// <summary>
        /// A tela de vendas grava o endereco como "rua, numero, bairro, cidade, UF, CEP",
        /// pulando as partes vazias. Le de tras para frente: CEP, UF e cidade
        /// sao reconheciveis; o resto e rua, numero e bairro.
        /// </summary>
        public static NfeEndereco LerEnderecoDaVenda(string? texto, string? ufPadrao)
        {
            var e = new NfeEndereco { Uf = ufPadrao };
            var partes = (texto ?? "").Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (partes.Count == 0) return e;

            if (System.Text.RegularExpressions.Regex.IsMatch(partes[^1], @"^\d{5}-?\d{3}$"))
            {
                e.Cep = NfeVeiculoBuilder.SoDigitos(partes[^1]);
                partes.RemoveAt(partes.Count - 1);
            }
            if (partes.Count > 0 && partes[^1].Length == 2 && EhUf(partes[^1]))
            {
                e.Uf = partes[^1].ToUpperInvariant();
                partes.RemoveAt(partes.Count - 1);
            }
            if (partes.Count > 1)
            {
                e.Municipio = partes[^1];
                partes.RemoveAt(partes.Count - 1);
            }
            if (partes.Count == 0) return e;

            e.Logradouro = partes[0];
            var resto = partes.Skip(1).ToList();
            if (resto.Count >= 2)
            {
                e.Numero = resto[0];
                e.Bairro = string.Join(", ", resto.Skip(1));
            }
            else if (resto.Count == 1)
            {
                var r = resto[0];
                if (char.IsDigit(r[0]) || r.StartsWith("S/N", StringComparison.OrdinalIgnoreCase)) e.Numero = r;
                else e.Bairro = r;
            }
            return e;
        }

        private static bool EhUf(string uf)
        {
            try { NfeVeiculoBuilder.Uf(uf); return true; }
            catch (NfeException) { return false; }
        }

        /// <summary>Acha o codigo IBGE pelo nome da cidade; sem acerto exato, deixa para a tela.</summary>
        private async Task CompletarMunicipioAsync(NfeEndereco e, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(e.Municipio) || string.IsNullOrWhiteSpace(e.Uf) || !EhUf(e.Uf)) return;
            try
            {
                var alvo = NfeVeiculoBuilder.SemAcento(e.Municipio).ToUpperInvariant();
                var achado = (await BuscarMunicipiosAsync(e.Uf, e.Municipio, ct))
                    .FirstOrDefault(m => NfeVeiculoBuilder.SemAcento(m.Nome).ToUpperInvariant() == alvo);
                if (achado != null)
                {
                    e.CodigoMunicipio = achado.Codigo;
                    e.Municipio = achado.Nome;
                }
            }
            catch (NfeException) { /* IBGE fora: a loja escolhe na tela */ }
        }

        private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        public async Task<NfeResumo> EmitirAsync(int veiculoId, NfeEmissaoInput input, CancellationToken ct = default)
        {
            var cfg = await ObterConfigAsync(ct);
            if (!cfg.Pronto)
                throw new NfeException("Complete a configuração da NF-e em Notas fiscais → Configuração: " + string.Join(", ", cfg.Pendencias) + ".");
            var tipo = Tipo(input.Tipo);
            Validar(input);
            _ = await _veiculos.GetByIdAsync(veiculoId) ?? throw new NfeException("Veículo não encontrado.");

            var existentes = await _notas.GetByVeiculoAsync(veiculoId);
            if (existentes.Any(n => n.NfTipo == tipo && n.NfStatus == NotaFiscal.StatusAutorizada))
                throw new NfeException($"Este veículo já tem NF-e de {(tipo == NotaFiscal.Entrada ? "compra" : "venda")} autorizada. Cancele-a antes de emitir outra.");
            if (existentes.FirstOrDefault(n => n.NfTipo == tipo && n.PodeReenviar) is { } pendente)
                return await ReenviarAsync(pendente.NfId, input, ct);

            var nota = await ReservarNumeroAsync(veiculoId, tipo, cfg, ct);
            return await EnviarAsync(nota, input, cfg, ct);
        }

        public async Task<NfeResumo> ReenviarAsync(int notaId, NfeEmissaoInput? correcao, CancellationToken ct = default)
        {
            var nota = await _notas.GetByIdAsync(notaId) ?? throw new NfeException("Nota não encontrada.");
            if (!nota.PodeReenviar) throw new NfeException("Só dá para reenviar nota pendente ou rejeitada.");
            var dados = correcao ?? (nota.NfDadosJson == null ? null : JsonSerializer.Deserialize<NfeEmissaoInput>(nota.NfDadosJson, Json))
                ?? throw new NfeException("Informe os dados da nota.");
            dados.Tipo = nota.NfTipo;
            Validar(dados);

            var cfg = await ObterConfigAsync(ct);
            if (!cfg.Pronto)
                throw new NfeException("Complete a configuração da NF-e em Notas fiscais → Configuração: " + string.Join(", ", cfg.Pendencias) + ".");

            // Nota que ja foi a Sefaz sem resposta final (ou que voltou como duplicada):
            // antes de mandar de novo, pergunta se ela ja foi autorizada.
            if (PrecisaConsultar(nota))
            {
                var resolvida = await ResolverPelaConsultaAsync(nota, ct);
                if (resolvida != null) return resolvida;
            }
            return await EnviarAsync(nota, dados, cfg, ct);
        }

        /// <summary>204 = duplicidade de NF-e (a mesma chave ja foi autorizada).</summary>
        private static bool PrecisaConsultar(NotaFiscal n)
            => n.NfChave != null && (n.NfStatus == NotaFiscal.StatusPendente || n.NfCStat == 204);

        /// <summary>
        /// Consulta a nota na Sefaz (botao "Consultar na Sefaz" e antes de reenviar).
        /// Autorizada la: guarda o protocolo e o XML, sem emitir outra.
        /// </summary>
        public async Task<NfeResumo> ConsultarAsync(int notaId, CancellationToken ct = default)
        {
            var nota = await _notas.GetByIdAsync(notaId) ?? throw new NfeException("Nota não encontrada.");
            if (nota.NfChave == null) throw new NfeException("Esta nota ainda não foi enviada à Sefaz.");
            return await ResolverPelaConsultaAsync(nota, ct) ?? Resumo(nota);
        }

        /// <summary>Devolve o resumo se a consulta resolveu a nota; nulo se ela nao existe la (pode reenviar).</summary>
        private async Task<NfeResumo?> ResolverPelaConsultaAsync(NotaFiscal nota, CancellationToken ct)
        {
            using var cert = await CertificadoObrigatorioAsync(ct);
            NfeRetornoConsulta r;
            try
            {
                r = _sefaz.Consultar(nota.NfChave!, nota.NfAmbiente != "producao", cert);
            }
            catch (Exception ex) when (ex is not NfeException)
            {
                _logger.LogWarning(ex, "NF-e {Chave}: falha ao consultar na Sefaz", nota.NfChave);
                throw new NfeException("Não foi possível consultar a nota na Sefaz agora. Ela continua pendente: tente de novo em alguns minutos.");
            }

            if (r.Autorizada && r.ProtocoloXml != null)
            {
                if (nota.NfXmlAssinado == null)
                    throw new NfeException($"A Sefaz já autorizou esta nota (protocolo {r.Protocolo}), mas o XML assinado não ficou guardado. Fale com o suporte antes de emitir outra.");
                nota.Autorizar(r.CStat ?? 100, r.Motivo, r.Protocolo ?? "", r.DataAutorizacao,
                    NfeSefazUnimake.MontarProc(nota.NfXmlAssinado, r.ProtocoloXml));
                await _notas.SalvarAsync(nota);
                _logger.LogInformation("NF-e {Chave} estava autorizada na Sefaz (consulta)", nota.NfChave);
                var resumo = Resumo(nota);
                resumo.Motivo = await InformarRenaveAsync(nota, ct) ?? resumo.Motivo;
                return resumo;
            }
            if (r.Cancelada)
                throw new NfeException($"Esta nota consta como cancelada na Sefaz ({r.CStat}: {r.Motivo}). Fale com o contador antes de emitir outra.");
            if (r.NaoExiste) return null;
            throw new NfeException($"A Sefaz respondeu à consulta ({r.CStat}): {r.Motivo}. A nota continua pendente.");
        }

        private async Task<NfeResumo> EnviarAsync(NotaFiscal nota, NfeEmissaoInput dados, NfeConfigInfo cfg, CancellationToken ct)
        {
            var veiculo = await _veiculos.GetByIdAsync(nota.R_VeiId) ?? throw new NfeException("Veículo não encontrado.");
            var homologacao = nota.NfAmbiente != "producao";
            var dataEmissao = DateTime.Now;
            var codigoNumerico = CodigoNumerico(nota.NfNumero);

            var xml = NfeVeiculoBuilder.Montar(cfg, dados, veiculo, nota.NfSerie, nota.NfNumero, codigoNumerico, dataEmissao, homologacao);
            var chave = xml.NFe[0].InfNFeField.Chave;
            nota.PrepararEnvio(chave, dados.Valor, NfeVeiculoBuilder.SoDigitos(dados.Documento), dados.Nome?.Trim(),
                JsonSerializer.Serialize(dados, Json), dataEmissao.ToUniversalTime());
            await _notas.SalvarAsync(nota);

            using var cert = await CertificadoObrigatorioAsync(ct);
            NfeRetornoAutorizacao ret;
            string? assinada = null;
            try
            {
                ret = _sefaz.Autorizar(xml, cert, x => assinada = x);
            }
            catch (NfeException ex)
            {
                nota.Rejeitar(null, ex.Message);
                await _notas.SalvarAsync(nota);
                throw;
            }
            catch (Exception ex)
            {
                // Sem resposta da Sefaz a nota fica PENDENTE: pode ter sido autorizada.
                // Guarda a NF-e assinada para, na consulta, montar o XML autorizado.
                if (assinada != null) nota.GuardarXmlAssinado(assinada);
                nota.FicarPendente(null, "Sem resposta da Sefaz.");
                await _notas.SalvarAsync(nota);
                _logger.LogWarning(ex, "NF-e {Chave}: falha de comunicacao com a Sefaz", chave);
                throw new NfeException("Não foi possível falar com a Sefaz. A nota ficou pendente: tente reenviar em alguns minutos (o sistema confere antes se ela já foi autorizada).");
            }
            if (assinada != null) nota.GuardarXmlAssinado(assinada);

            if (ret.Pendente)
            {
                nota.FicarPendente(ret.CStat, $"{ret.CStat} - {ret.Motivo}");
                await _notas.SalvarAsync(nota);
                throw new NfeException($"A Sefaz recebeu a nota mas ainda não respondeu ({ret.CStat}: {ret.Motivo}). Use \"Consultar na Sefaz\" em alguns minutos.");
            }

            if (ret.Autorizada && ret.XmlProc != null)
            {
                nota.Autorizar(ret.CStat ?? 100, ret.Motivo, ret.Protocolo ?? "", ret.DataAutorizacao, ret.XmlProc);
                await _notas.SalvarAsync(nota);
                _logger.LogInformation("NF-e {Chave} autorizada (protocolo {Protocolo})", chave, ret.Protocolo);
                var resumo = Resumo(nota);
                resumo.Motivo = await InformarRenaveAsync(nota, ct) ?? resumo.Motivo;
                return resumo;
            }

            nota.Rejeitar(ret.CStat, $"{ret.CStat} - {ret.Motivo}");
            await _notas.SalvarAsync(nota);
            _logger.LogWarning("NF-e {Chave} rejeitada: {CStat} {Motivo}", chave, ret.CStat, ret.Motivo);

            // Duplicidade: a mesma nota ja tinha sido autorizada (a resposta anterior se perdeu).
            if (ret.CStat == 204 && await ResolverPelaConsultaAsync(nota, ct) is { } jaAutorizada) return jaAutorizada;
            throw new NfeException($"A Sefaz rejeitou a nota ({ret.CStat}): {ret.Motivo}");
        }

        public async Task<NfeResumo> CancelarAsync(int notaId, string justificativa, CancellationToken ct = default)
        {
            var nota = await _notas.GetByIdAsync(notaId) ?? throw new NfeException("Nota não encontrada.");
            if (nota.NfStatus != NotaFiscal.StatusAutorizada || nota.NfProtocolo == null || nota.NfChave == null)
                throw new NfeException("Só dá para cancelar nota autorizada.");
            if (DateTime.UtcNow - (nota.NfDtAutorizacao ?? nota.NfDtEmissao) > PrazoCancelamento)
                throw new NfeException("Passou o prazo de 24 horas para cancelar. Fale com o contador (cancelamento extemporâneo ou nota de devolução).");
            justificativa = (justificativa ?? "").Trim();
            if (justificativa.Length is < 15 or > 255)
                throw new NfeException("A justificativa precisa ter entre 15 e 255 caracteres.");

            var cfg = await ObterConfigAsync(ct);
            using var cert = await CertificadoObrigatorioAsync(ct);
            NfeRetornoEvento ret;
            try
            {
                ret = _sefaz.Cancelar(nota.NfChave, nota.NfProtocolo, NfeVeiculoBuilder.SemAcento(justificativa),
                    NfeVeiculoBuilder.SoDigitos(cfg.Cnpj), NfeVeiculoBuilder.Uf(cfg.Uf), nota.NfAmbiente != "producao", cert);
            }
            catch (Exception ex) when (ex is not NfeException)
            {
                _logger.LogWarning(ex, "NF-e {Chave}: falha ao cancelar", nota.NfChave);
                throw new NfeException("Não foi possível falar com a Sefaz para cancelar. Tente de novo em alguns minutos.");
            }

            if (!ret.Registrado)
                throw new NfeException($"A Sefaz recusou o cancelamento ({ret.CStat}): {ret.Motivo}");

            nota.Cancelar(ret.Protocolo ?? "", ret.Data, ret.Xml ?? "");
            await _notas.SalvarAsync(nota);
            _logger.LogInformation("NF-e {Chave} cancelada (protocolo {Protocolo})", nota.NfChave, ret.Protocolo);
            return Resumo(nota);
        }

        // ================================================================
        // Carta de correcao (CC-e)
        // ================================================================

        /// <summary>
        /// Corrige dado da nota autorizada que nao mexe em valores, impostos, data
        /// nem em quem vende/compra. Cada carta substitui a anterior, entao o
        /// texto precisa trazer todas as correcoes. Ate 20 por nota.
        /// </summary>
        public async Task<NfeResumo> CorrigirAsync(int notaId, string correcao, CancellationToken ct = default)
        {
            var nota = await _notas.GetByIdAsync(notaId) ?? throw new NfeException("Nota não encontrada.");
            if (nota.NfStatus != NotaFiscal.StatusAutorizada || nota.NfChave == null)
                throw new NfeException("Só dá para corrigir nota autorizada (e não cancelada).");
            var texto = string.Join(' ', NfeVeiculoBuilder.SemAcento(correcao ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (texto.Length is < 15 or > 1000)
                throw new NfeException("A correção precisa ter entre 15 e 1000 caracteres.");
            var sequencia = nota.NfCceSequencia + 1;
            if (sequencia > NotaFiscal.MaximoCartasCorrecao)
                throw new NfeException($"Esta nota já tem {NotaFiscal.MaximoCartasCorrecao} cartas de correção, o limite da Sefaz.");

            var cfg = await ObterConfigAsync(ct);
            using var cert = await CertificadoObrigatorioAsync(ct);
            NfeRetornoEvento ret;
            try
            {
                ret = _sefaz.CartaCorrecao(nota.NfChave, sequencia, texto, NfeVeiculoBuilder.SoDigitos(cfg.Cnpj),
                    NfeVeiculoBuilder.Uf(cfg.Uf), nota.NfAmbiente != "producao", cert);
            }
            catch (Exception ex) when (ex is not NfeException)
            {
                _logger.LogWarning(ex, "NF-e {Chave}: falha na carta de correcao", nota.NfChave);
                throw new NfeException("Não foi possível falar com a Sefaz para a carta de correção. Tente de novo em alguns minutos.");
            }

            if (!ret.Registrado)
                throw new NfeException($"A Sefaz recusou a carta de correção ({ret.CStat}): {ret.Motivo}");

            nota.RegistrarCartaCorrecao(sequencia, texto, ret.Protocolo ?? "", ret.Data, ret.Xml ?? "");
            await _notas.SalvarAsync(nota);
            _logger.LogInformation("NF-e {Chave}: carta de correcao {Seq} registrada", nota.NfChave, sequencia);
            return Resumo(nota);
        }

        public async Task<(string Xml, string NomeArquivo)> ObterXmlCartaCorrecaoAsync(int notaId, CancellationToken ct = default)
        {
            var nota = await _notas.GetByIdAsync(notaId) ?? throw new NfeException("Nota não encontrada.");
            if (string.IsNullOrEmpty(nota.NfCceXml)) throw new NfeException("Esta nota não tem carta de correção.");
            return (nota.NfCceXml, $"{nota.NfChave}-cce-{nota.NfCceSequencia}.xml");
        }

        // ================================================================
        // Inutilizacao de numeracao
        // ================================================================

        public async Task<IReadOnlyList<NfeInutilizacaoInfo>> ListarInutilizacoesAsync(CancellationToken ct = default)
            => (await _notas.ListarInutilizacoesAsync()).Select(Info).ToList();

        /// <summary>
        /// Avisa a Sefaz que uma faixa de numeros da serie nao vai ser usada (nota
        /// rejeitada que nao sera reenviada, numero pulado). Nao serve para nota
        /// autorizada (essa se cancela) nem pendente (pode ter sido autorizada).
        /// </summary>
        public async Task<NfeInutilizacaoInfo> InutilizarAsync(NfeInutilizacaoInput input, CancellationToken ct = default)
        {
            var cfg = await ObterConfigAsync(ct);
            if (!cfg.Pronto)
                throw new NfeException("Complete a configuração da NF-e em Notas fiscais → Configuração: " + string.Join(", ", cfg.Pendencias) + ".");
            if (input.Serie is < 0 or > 999) throw new NfeException("Série deve ficar entre 0 e 999.");
            if (input.NumeroInicial < 1 || input.NumeroFinal < input.NumeroInicial || input.NumeroFinal > 999_999_999)
                throw new NfeException("Faixa de números inválida.");
            if (input.NumeroFinal - input.NumeroInicial >= 1000)
                throw new NfeException("Inutilize no máximo 1000 números por vez.");
            var justificativa = NfeVeiculoBuilder.SemAcento((input.Justificativa ?? "").Trim());
            if (justificativa.Length is < 15 or > 255)
                throw new NfeException("A justificativa precisa ter entre 15 e 255 caracteres.");

            var ambiente = cfg.Ambiente;
            var notas = await _notas.GetNoIntervaloAsync(ambiente, input.Serie, input.NumeroInicial, input.NumeroFinal);
            var bloqueadas = notas.Where(n => n.NfStatus != NotaFiscal.StatusRejeitada).OrderBy(n => n.NfNumero).ToList();
            if (bloqueadas.Count > 0)
            {
                var n = bloqueadas[0];
                throw new NfeException(n.NfStatus switch
                {
                    NotaFiscal.StatusAutorizada or NotaFiscal.StatusCancelada =>
                        $"O número {n.NfNumero} já é de uma nota autorizada: nota autorizada se cancela, não se inutiliza.",
                    NotaFiscal.StatusPendente =>
                        $"A nota nº {n.NfNumero} está pendente (pode ter sido autorizada). Reenvie-a antes de inutilizar.",
                    _ => $"O número {n.NfNumero} já foi inutilizado."
                });
            }
            var jaInutilizada = (await _notas.ListarInutilizacoesAsync()).FirstOrDefault(i => i.NfiAmbiente == ambiente
                && i.NfiSerie == input.Serie && i.NfiNumeroInicial <= input.NumeroFinal && i.NfiNumeroFinal >= input.NumeroInicial);
            if (jaInutilizada != null)
                throw new NfeException($"Parte desta faixa já foi inutilizada ({jaInutilizada.NfiNumeroInicial} a {jaInutilizada.NfiNumeroFinal}).");

            using var cert = await CertificadoObrigatorioAsync(ct);
            NfeRetornoEvento ret;
            try
            {
                ret = _sefaz.Inutilizar(input.Serie, input.NumeroInicial, input.NumeroFinal, justificativa,
                    NfeVeiculoBuilder.SoDigitos(cfg.Cnpj), NfeVeiculoBuilder.Uf(cfg.Uf), ambiente != "producao", cert);
            }
            catch (Exception ex) when (ex is not NfeException)
            {
                _logger.LogWarning(ex, "NF-e: falha ao inutilizar {Ini}-{Fim} da serie {Serie}", input.NumeroInicial, input.NumeroFinal, input.Serie);
                throw new NfeException("Não foi possível falar com a Sefaz para inutilizar. Tente de novo em alguns minutos.");
            }
            if (!ret.Registrado)
                throw new NfeException($"A Sefaz recusou a inutilização ({ret.CStat}): {ret.Motivo}");

            var inut = new NotaFiscalInutilizacao(ambiente, input.Serie, input.NumeroInicial, input.NumeroFinal, justificativa,
                ret.Protocolo, ret.Data ?? DateTime.UtcNow, ret.Xml);
            await _notas.SalvarInutilizacaoAsync(inut);
            foreach (var rejeitada in notas)
            {
                rejeitada.Inutilizar($"Número inutilizado na Sefaz (protocolo {ret.Protocolo}).");
                await _notas.SalvarAsync(rejeitada);
            }
            _logger.LogInformation("NF-e: numeros {Ini}-{Fim} da serie {Serie} inutilizados (protocolo {Protocolo})",
                input.NumeroInicial, input.NumeroFinal, input.Serie, ret.Protocolo);
            return Info(inut);
        }

        public async Task<(string Xml, string NomeArquivo)> ObterXmlInutilizacaoAsync(int id, CancellationToken ct = default)
        {
            var inut = await _notas.ObterInutilizacaoAsync(id) ?? throw new NfeException("Inutilização não encontrada.");
            if (string.IsNullOrEmpty(inut.NfiXml)) throw new NfeException("Esta inutilização não tem XML.");
            return (inut.NfiXml, $"inutilizacao-serie{inut.NfiSerie}-{inut.NfiNumeroInicial}-{inut.NfiNumeroFinal}.xml");
        }

        private static NfeInutilizacaoInfo Info(NotaFiscalInutilizacao i) => new()
        {
            Id = i.NfiId,
            Ambiente = i.NfiAmbiente,
            Serie = i.NfiSerie,
            NumeroInicial = i.NfiNumeroInicial,
            NumeroFinal = i.NfiNumeroFinal,
            Justificativa = i.NfiJustificativa,
            Protocolo = i.NfiProtocolo,
            Data = i.NfiData
        };

        public async Task<(string Xml, string NomeArquivo)> ObterXmlAsync(int notaId, CancellationToken ct = default)
        {
            var nota = await _notas.GetByIdAsync(notaId) ?? throw new NfeException("Nota não encontrada.");
            if (string.IsNullOrEmpty(nota.NfXml)) throw new NfeException("Esta nota ainda não tem XML autorizado.");
            return (nota.NfXml, $"{nota.NfChave}-procNFe.xml");
        }

        public async Task<(byte[] Pdf, string NomeArquivo)> ObterDanfeAsync(int notaId, CancellationToken ct = default)
        {
            var nota = await _notas.GetByIdAsync(notaId) ?? throw new NfeException("Nota não encontrada.");
            if (string.IsNullOrEmpty(nota.NfXml)) throw new NfeException("O DANFE só existe para nota autorizada.");
            var pdf = DanfeGerador.Gerar(nota.NfXml, cancelada: nota.NfStatus == NotaFiscal.StatusCancelada);
            return (pdf, $"DANFE-{nota.NfNumero}.pdf");
        }

        // ================================================================
        // Auxiliares
        // ================================================================

        private async Task<NotaFiscal> ReservarNumeroAsync(int veiculoId, string tipo, NfeConfigInfo cfg, CancellationToken ct)
        {
            var trava = TravasNumeracao.GetOrAdd(_tenant.TenantSlug ?? "", _ => new SemaphoreSlim(1, 1));
            await trava.WaitAsync(ct);
            try
            {
                // Reler dentro da trava: o numero pode ter andado.
                var atual = await ObterConfigAsync(ct);
                var numero = Math.Max(1, atual.ProximoNumero);
                while (await _notas.NumeroEmUsoAsync(cfg.Ambiente, atual.Serie, numero)) numero++;

                atual.ProximoNumero = numero + 1;
                await SalvarConfigAsync(atual, ct);

                var nota = new NotaFiscal(veiculoId, tipo, cfg.Ambiente, atual.Serie, numero);
                await _notas.SalvarAsync(nota);
                return nota;
            }
            finally
            {
                trava.Release();
            }
        }

        /// <summary>cNF: 8 digitos aleatorios, diferente do numero da nota (regra da Sefaz).</summary>
        public static int CodigoNumerico(int numero)
        {
            int c;
            do c = RandomNumberGenerator.GetInt32(10_000_000, 100_000_000);
            while (c == numero);
            return c;
        }

        private async Task<System.Security.Cryptography.X509Certificates.X509Certificate2> CertificadoObrigatorioAsync(CancellationToken ct)
        {
            try
            {
                return await _certificado.CarregarAsync(ct)
                    ?? throw new NfeException("Envie o certificado A1 da loja em Notas fiscais → Configuração.");
            }
            catch (CertificadoDigitalException ex)
            {
                throw new NfeException(ex.Message);
            }
        }

        /// <summary>Manda a chave autorizada para o Renave; devolve aviso se nao deu.</summary>
        private async Task<string?> InformarRenaveAsync(NotaFiscal nota, CancellationToken ct)
        {
            try
            {
                var s = await _renave.ObterSituacaoAsync(nota.R_VeiId, ct);
                if (nota.NfTipo == NotaFiscal.Entrada && s.IdEstoque != null && s.ChaveNfEntrada == null)
                    await _renave.EnviarNotaFiscalAsync(nota.R_VeiId, "COMPRA", nota.NfChave!, ct);
                else if (nota.NfTipo == NotaFiscal.Saida && s.DataSaida != null && s.ChaveNfSaida == null)
                    await _renave.EnviarNotaFiscalAsync(nota.R_VeiId, "VENDA", nota.NfChave!, ct);
                return null;
            }
            catch (RenaveException ex)
            {
                return $"Nota autorizada, mas o Renave recusou a chave: {ex.Message}";
            }
        }

        private static string Tipo(string? tipo)
            => string.Equals(tipo, NotaFiscal.Entrada, StringComparison.OrdinalIgnoreCase) ? NotaFiscal.Entrada
             : string.Equals(tipo, NotaFiscal.Saida, StringComparison.OrdinalIgnoreCase) ? NotaFiscal.Saida
             : throw new NfeException("Tipo da nota inválido (ENTRADA ou SAIDA).");

        private static void Validar(NfeEmissaoInput d)
        {
            var doc = NfeVeiculoBuilder.SoDigitos(d.Documento);
            if (!Renave.RenaveService.DocumentoValido(doc)) throw new NfeException("CPF/CNPJ inválido.");
            if (string.IsNullOrWhiteSpace(d.Nome)) throw new NfeException("Informe o nome.");
            if (d.Valor <= 0) throw new NfeException("Informe o valor do veículo.");
            var e = d.Endereco ?? throw new NfeException("Informe o endereço.");
            if (string.IsNullOrWhiteSpace(e.Logradouro) || string.IsNullOrWhiteSpace(e.Bairro))
                throw new NfeException("Informe o endereço (rua e bairro).");
            if (e.CodigoMunicipio < 1000000 || string.IsNullOrWhiteSpace(e.Municipio))
                throw new NfeException("Escolha o município.");
            NfeVeiculoBuilder.Uf(e.Uf);
            var ncm = NfeVeiculoBuilder.SoDigitos(d.Ncm);
            if (ncm.Length is > 0 and not 8) throw new NfeException("NCM deve ter 8 dígitos.");
        }

        public static NfeResumo Resumo(NotaFiscal n) => new()
        {
            Id = n.NfId,
            VeiculoId = n.R_VeiId,
            Tipo = n.NfTipo,
            Ambiente = n.NfAmbiente,
            Serie = n.NfSerie,
            Numero = n.NfNumero,
            Chave = n.NfChave,
            Status = n.NfStatus,
            CStat = n.NfCStat,
            Motivo = n.NfMotivo,
            Protocolo = n.NfProtocolo,
            DataEmissao = n.NfDtEmissao,
            DataAutorizacao = n.NfDtAutorizacao,
            Valor = n.NfValor,
            DestinatarioNome = n.NfDestNome,
            DataCancelamento = n.NfDtCancelamento,
            PodeReenviar = n.PodeReenviar,
            PodeCancelar = n.NfStatus == NotaFiscal.StatusAutorizada
                && DateTime.UtcNow - (n.NfDtAutorizacao ?? n.NfDtEmissao) <= PrazoCancelamento,
            CartaCorrecaoSequencia = n.NfCceSequencia,
            CartaCorrecaoTexto = n.NfCceTexto,
            CartaCorrecaoData = n.NfCceData,
            PodeCorrigir = n.NfStatus == NotaFiscal.StatusAutorizada && n.NfCceSequencia < NotaFiscal.MaximoCartasCorrecao
        };
    }

    internal static class HttpIbge
    {
        internal sealed record MunicipioIbge(int Id, string Nome);

        internal static async Task<List<MunicipioIbge>> GetFromJsonAsyncCompat(this HttpClient http, string url, CancellationToken ct)
        {
            using var resp = await http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<List<MunicipioIbge>>(s, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct) ?? new();
        }
    }
}
