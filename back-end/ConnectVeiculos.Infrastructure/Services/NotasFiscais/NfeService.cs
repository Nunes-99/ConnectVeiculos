using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.NotasFiscais;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.NotasFiscais;
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
        private readonly ICertificadoDigitalService _certificado;
        private readonly INfeSefaz _sefaz;
        private readonly IRenaveService _renave;
        private readonly ITenantContext _tenant;
        private readonly HttpClient _http;
        private readonly ILogger<NfeService> _logger;

        public NfeService(IConfiguracaoSistemaRepository config, INotaFiscalRepository notas, IVeiculoRepository veiculos,
            ICertificadoDigitalService certificado, INfeSefaz sefaz, IRenaveService renave, ITenantContext tenant,
            HttpClient http, ILogger<NfeService> logger)
        {
            _config = config;
            _notas = notas;
            _veiculos = veiculos;
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

        public async Task<NfeResumo> EmitirAsync(int veiculoId, NfeEmissaoInput input, CancellationToken ct = default)
        {
            var cfg = await ObterConfigAsync(ct);
            if (!cfg.Pronto)
                throw new NfeException("Complete a configuração da NF-e em Integrações: " + string.Join(", ", cfg.Pendencias) + ".");
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
                throw new NfeException("Complete a configuração da NF-e em Integrações: " + string.Join(", ", cfg.Pendencias) + ".");
            return await EnviarAsync(nota, dados, cfg, ct);
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
            try
            {
                ret = _sefaz.Autorizar(xml, cert);
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
                _logger.LogWarning(ex, "NF-e {Chave}: falha de comunicacao com a Sefaz", chave);
                throw new NfeException("Não foi possível falar com a Sefaz. A nota ficou pendente: tente reenviar em alguns minutos.");
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

        public async Task<(string Xml, string NomeArquivo)> ObterXmlAsync(int notaId, CancellationToken ct = default)
        {
            var nota = await _notas.GetByIdAsync(notaId) ?? throw new NfeException("Nota não encontrada.");
            if (string.IsNullOrEmpty(nota.NfXml)) throw new NfeException("Esta nota ainda não tem XML autorizado.");
            return (nota.NfXml, $"{nota.NfChave}-procNFe.xml");
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
                    ?? throw new NfeException("Envie o certificado A1 da loja em Integrações.");
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
                && DateTime.UtcNow - (n.NfDtAutorizacao ?? n.NfDtEmissao) <= PrazoCancelamento
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
