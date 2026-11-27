using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Entities.Consultas;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.ConsultaPlaca
{
    public class PrecoUfInfo
    {
        public string Uf { get; set; } = "";
        public decimal Preco { get; set; }
        public List<string> Requer { get; set; } = new();
    }

    public class ServicoConsulta
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        /// <summary>CONSULTAS, DOCUMENTOS ou ANALISE_PESSOA.</summary>
        public string Categoria { get; set; } = "";
        public string? Prazo { get; set; }
        public string? Nota { get; set; }
        public List<string> Requer { get; set; } = new();
        /// <summary>Preco para a loja (null no CRLV: ver PrecosUf).</summary>
        public decimal? Preco { get; set; }
        public List<PrecoUfInfo> PrecosUf { get; set; } = new();
        /// <summary>Dado de pessoa: exige finalidade e o aceite da LGPD.</summary>
        public bool ExigeFinalidade { get; set; }
        public bool Recomendado { get; set; }
    }

    /// <summary>O que a tela manda para consultar.</summary>
    public class PedidoConsulta
    {
        public string Servico { get; set; } = "";
        public string? Placa { get; set; }
        public string? Uf { get; set; }
        public string? Renavam { get; set; }
        public string? Cpf { get; set; }
        public string? Chassi { get; set; }
        public string? Motor { get; set; }
        public string? Documento { get; set; }
        public string? Cnh { get; set; }
        public string? Chave { get; set; }
        public string? DataNascimento { get; set; }
        public string? Finalidade { get; set; }
        public bool DeclaracaoFinalidade { get; set; }
        /// <summary>Versao do aviso do ATPV-e que o usuario aceitou.</summary>
        public string? AceiteAtpveVersao { get; set; }
    }

    public class ConsultaItem
    {
        public int Id { get; set; }
        public string Placa { get; set; } = "";
        public string? Identificador { get; set; }
        public string Servico { get; set; } = "";
        public string ServicoNome { get; set; } = "";
        public string Categoria { get; set; } = "";
        /// <summary>PROCESSANDO, CONCLUIDA ou ERRO.</summary>
        public string Status { get; set; } = "";
        public decimal Valor { get; set; }
        public string? Erro { get; set; }
        public bool Estornada { get; set; }
        public bool Teste { get; set; }
        public bool TemDocumento { get; set; }
        public string? Usuario { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime? ConcluidoEm { get; set; }
        /// <summary>Bloco "veiculo" da ZapCar (snake_case), quando o servico traz.</summary>
        public JsonElement? Veiculo { get; set; }
        /// <summary>Bloco "dados" da ZapCar, como a base devolveu (varia por servico).</summary>
        public JsonElement? Dados { get; set; }
        public List<AlertaConsulta> Alertas { get; set; } = new();
        /// <summary>So faz sentido nas consultas de veiculo com o bloco "veiculo".</summary>
        public bool NadaConsta => Status == ConsultaVeicular.Concluida && Veiculo.HasValue && Alertas.Count == 0;
    }

    public class ConsultaPlacaInfo
    {
        public bool Disponivel { get; set; }
        /// <summary>Chave de teste: placas TST0001..TST0005, dados ficticios.</summary>
        public bool Teste { get; set; }
        public decimal Saldo { get; set; }
        public string? Plano { get; set; }
        public List<ServicoConsulta> Servicos { get; set; } = new();
        public IReadOnlyList<decimal> Pacotes { get; set; } = Array.Empty<decimal>();
        public List<ConsultaItem> Consultas { get; set; } = new();
        public IReadOnlyList<MovimentoCredito> Extrato { get; set; } = Array.Empty<MovimentoCredito>();
    }

    /// <summary>
    /// Consultas da ZapCar (placa, documentos do veiculo e dados de pessoa):
    /// debita os creditos da loja pelo preco do plano dela, pede a ZapCar e
    /// guarda o resultado no banco da loja. Se a ZapCar recusar ou a consulta
    /// terminar em erro, o valor volta para os creditos (a ZapCar tambem nos devolve).
    /// </summary>
    public class ConsultaPlacaService
    {
        public const string Completa = "consulta-completa";
        public const string Simples = "consulta";

        public static readonly string[] Finalidades = { "NEGOCIACAO_VEICULO", "ANALISE_CREDITO", "VERIFICACAO_CADASTRAL", "OUTRA" };
        private static readonly string[] Ufs =
        {
            "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT", "PA", "PB", "PE", "PI",
            "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO"
        };
        private static readonly Regex FormatoPlaca = new("^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$", RegexOptions.Compiled);
        private static readonly CultureInfo PtBr = new("pt-BR");

        private readonly ConnectVeiculosDbContext _db;
        private readonly ICreditosConsultaService _creditos;
        private readonly IZapCarClient _zapcar;
        private readonly PrecosConsultaService _precos;
        private readonly MasterDbContext _master;
        private readonly ZapCarSettings _settings;
        private readonly ILogger<ConsultaPlacaService> _logger;

        public ConsultaPlacaService(ConnectVeiculosDbContext db, ICreditosConsultaService creditos, IZapCarClient zapcar,
            PrecosConsultaService precos, MasterDbContext master, IOptions<ZapCarSettings> settings, ILogger<ConsultaPlacaService> logger)
        {
            _db = db;
            _creditos = creditos;
            _zapcar = zapcar;
            _precos = precos;
            _master = master;
            _settings = settings.Value;
            _logger = logger;
        }

        public static bool ExigeFinalidade(ZapCarServico s) => s.Categoria == "ANALISE_PESSOA" || s.Id == "comunicacao-venda";

        /// <summary>Catalogo da ZapCar com o preco do plano da loja.</summary>
        public async Task<(List<ServicoConsulta> Servicos, string? Plano)> ServicosAsync(int tenantId, CancellationToken ct = default)
        {
            var catalogo = await _zapcar.ServicosAsync(ct);
            var plano = await PlanoDaLojaAsync(tenantId, ct);
            var preco = await _precos.TabelaAsync(plano.Id, plano.Nome, ct);

            var lista = catalogo.Select(s => new ServicoConsulta
            {
                Id = s.Id,
                Nome = s.Nome,
                Descricao = s.Descricao,
                Categoria = s.Categoria,
                Prazo = s.Prazo,
                Nota = s.Nota,
                Requer = s.Requer,
                Preco = s.PorUf.Count > 0 ? null : preco(s, null),
                PrecosUf = s.PorUf.Select(u => new PrecoUfInfo { Uf = u.Uf, Preco = preco(s, u.Uf) ?? 0, Requer = u.Requer }).ToList(),
                ExigeFinalidade = ExigeFinalidade(s),
                Recomendado = s.Id == Completa
            }).Where(s => s.Preco > 0 || s.PrecosUf.Count > 0).ToList();
            return (lista, plano.Nome);
        }

        private async Task<(int? Id, string? Nome)> PlanoDaLojaAsync(int tenantId, CancellationToken ct)
        {
            var planoId = await _master.Tenants.AsNoTracking().Where(t => t.TenId == tenantId).Select(t => t.TenPlaId).FirstOrDefaultAsync(ct);
            if (planoId == null) return (null, null);
            var nome = await _master.Planos.AsNoTracking().Where(p => p.PlaId == planoId).Select(p => p.PlaNome).FirstOrDefaultAsync(ct);
            return (planoId, nome);
        }

        public static string NormalizarPlaca(string? placa)
            => new string((placa ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static bool PlacaValida(string placa) => FormatoPlaca.IsMatch(placa);

        private static string Digitos(string? v) => new string((v ?? "").Where(char.IsDigit).ToArray());

        public async Task<ConsultaPlacaInfo> PainelAsync(int tenantId, CancellationToken ct = default)
        {
            // Atualiza as que ficaram processando (ex.: a tela foi fechada no meio).
            var pendentes = await _db.ConsultasVeiculares
                .Where(c => c.CvcStatus == ConsultaVeicular.Processando && c.CvcExternoId != null)
                .OrderByDescending(c => c.CvcId).Take(5).ToListAsync(ct);
            foreach (var p in pendentes) await AtualizarAsync(tenantId, p, ct);

            var consultas = await _db.ConsultasVeiculares.AsNoTracking()
                .OrderByDescending(c => c.CvcId).Take(50).ToListAsync(ct);
            var (servicos, plano) = _settings.Configurado ? await ServicosAsync(tenantId, ct) : (new List<ServicoConsulta>(), null);

            return new ConsultaPlacaInfo
            {
                Disponivel = _settings.Configurado && servicos.Count > 0,
                Teste = _settings.Teste,
                Saldo = await _creditos.SaldoAsync(tenantId, ct),
                Plano = plano,
                Servicos = servicos,
                Pacotes = _settings.ValoresPacotes(),
                Consultas = await ItensAsync(consultas, ct),
                Extrato = await _creditos.ExtratoAsync(tenantId, 20, ct)
            };
        }

        public async Task<ConsultaItem> ConsultarAsync(int tenantId, PedidoConsulta pedido, int usuarioId, CancellationToken ct = default)
        {
            if (!_settings.Configurado)
                throw new DomainException("A consulta ainda não está disponível.");

            var catalogo = await _zapcar.ServicosAsync(ct);
            var servico = catalogo.FirstOrDefault(s => s.Id == pedido.Servico)
                ?? throw new DomainException("Escolha o tipo de consulta.");

            var (zap, identificador, placa) = Validar(servico, pedido);

            var plano = await PlanoDaLojaAsync(tenantId, ct);
            var tabela = await _precos.TabelaAsync(plano.Id, plano.Nome, ct);
            var preco = tabela(servico, zap.Uf);
            if (preco is not > 0)
                throw new DomainException(servico.PorUf.Count > 0 ? "Este serviço não está disponível para o estado escolhido." : "Este serviço está indisponível no momento.");

            var referencia = Guid.NewGuid().ToString("N");
            var alvo = string.IsNullOrEmpty(placa) ? identificador : placa;
            var descricao = $"{servico.Nome} {alvo}".Trim();
            if (descricao.Length > 190) descricao = descricao[..190];
            if (!await _creditos.DebitarAsync(tenantId, preco.Value, referencia, descricao, ct))
                throw new DomainException($"Créditos insuficientes: esta consulta custa {preco.Value.ToString("C", PtBr)}. Compre créditos para continuar.");

            var consulta = new ConsultaVeicular(placa, servico.Id, referencia, preco.Value, usuarioId,
                string.IsNullOrEmpty(placa) ? identificador : null, zap.Finalidade);
            try
            {
                var criada = await _zapcar.CriarAsync(zap, referencia, ct);
                consulta.Enviada(criada.Id, criada.ValorCobrado, criada.Teste);
            }
            catch (Exception ex) when (ex is ZapCarException or HttpRequestException or TaskCanceledException)
            {
                await _creditos.EstornarAsync(tenantId, preco.Value, referencia, $"Devolução: {descricao}", CancellationToken.None);
                var mensagem = ex is ZapCarException z ? z.Message : "A base de consulta não respondeu. Tente de novo em alguns minutos.";
                consulta.Falhar(mensagem, estornada: true);
                _db.ConsultasVeiculares.Add(consulta);
                await _db.SaveChangesAsync(CancellationToken.None);
                _logger.LogWarning(ex, "Consulta {Servico} {Alvo} recusada; creditos devolvidos", servico.Id, alvo);
                throw new DomainException(mensagem + " Nada foi cobrado.");
            }

            _db.ConsultasVeiculares.Add(consulta);
            await _db.SaveChangesAsync(ct);
            return (await ItensAsync(new[] { consulta }, ct))[0];
        }

        /// <summary>
        /// Confere os campos que o servico exige antes de cobrar e monta o
        /// pedido. Devolve tambem o que mostrar no historico (CPF mascarado).
        /// </summary>
        public static (ZapCarPedido Pedido, string Identificador, string Placa) Validar(ZapCarServico servico, PedidoConsulta p)
        {
            var zap = new ZapCarPedido { Servico = servico.Id };
            var requer = new HashSet<string>(servico.Requer);
            var identificador = "";
            var placa = "";

            var uf = (p.Uf ?? "").Trim().ToUpperInvariant();
            if (servico.PorUf.Count > 0)
            {
                var porUf = servico.PorUf.FirstOrDefault(u => u.Uf == uf)
                    ?? throw new DomainException("Escolha um estado disponível para este serviço.");
                foreach (var r in porUf.Requer) requer.Add(r);
            }

            if (requer.Contains("placa"))
            {
                placa = NormalizarPlaca(p.Placa);
                if (!PlacaValida(placa)) throw new DomainException("Placa inválida. Use o formato ABC1234 ou ABC1D23.");
                zap.Placa = placa;
            }
            if (requer.Contains("uf"))
            {
                if (!Ufs.Contains(uf)) throw new DomainException("Escolha o estado (UF).");
                zap.Uf = uf;
            }
            if (requer.Contains("renavam"))
            {
                var renavam = Digitos(p.Renavam);
                if (renavam.Length is < 9 or > 11) throw new DomainException("Informe o Renavam (9 a 11 números).");
                zap.Renavam = renavam;
            }
            if (requer.Contains("cpf"))
            {
                var cpf = Digitos(p.Cpf);
                if (cpf.Length is not (11 or 14)) throw new DomainException("Informe o CPF ou CNPJ do proprietário.");
                zap.Cpf = cpf;
            }
            if (requer.Contains("chassi"))
            {
                var chassi = NormalizarPlaca(p.Chassi);
                if (chassi.Length != 17) throw new DomainException("O chassi tem 17 caracteres.");
                zap.Chassi = identificador = chassi;
            }
            if (requer.Contains("motor"))
            {
                var motor = NormalizarPlaca(p.Motor);
                if (motor.Length < 5) throw new DomainException("Informe o número do motor.");
                zap.Motor = identificador = motor;
            }
            if (requer.Contains("documento"))
            {
                var doc = Digitos(p.Documento);
                var soCpf = servico.Id.EndsWith("-cpf", StringComparison.Ordinal) || servico.Id is "situacao-cpf" or "cnh-nacional";
                var soCnpj = servico.Id.EndsWith("-cnpj", StringComparison.Ordinal);
                if (soCpf && doc.Length != 11) throw new DomainException("Informe um CPF com 11 números.");
                if (soCnpj && doc.Length != 14) throw new DomainException("Informe um CNPJ com 14 números.");
                if (doc.Length is not (11 or 14)) throw new DomainException("Informe um CPF (11 números) ou CNPJ (14 números).");
                zap.Documento = doc;
                identificador = MascararDocumento(doc);
            }
            if (requer.Contains("cnh"))
            {
                var cnh = Digitos(p.Cnh);
                if (cnh.Length is < 9 or > 11) throw new DomainException("Informe o número de registro da CNH.");
                zap.Cnh = cnh;
            }
            if (requer.Contains("chave"))
            {
                var chave = Digitos(p.Chave);
                if (chave.Length != 44) throw new DomainException("A chave de acesso da NF-e tem 44 números.");
                zap.Chave = identificador = chave;
            }
            if (requer.Contains("data_nascimento"))
            {
                if (!DateTime.TryParseExact((p.DataNascimento ?? "").Trim(), new[] { "dd/MM/yyyy", "yyyy-MM-dd" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var nasc) || nasc.Year < 1900 || nasc > DateTime.Today)
                    throw new DomainException("Informe a data de nascimento.");
                zap.DataNascimento = nasc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }

            if (ExigeFinalidade(servico))
            {
                if (!Finalidades.Contains(p.Finalidade))
                    throw new DomainException("Escolha a finalidade da consulta (exigência da LGPD).");
                if (!p.DeclaracaoFinalidade)
                    throw new DomainException("Confirme que os dados serão usados só para a finalidade informada.");
                zap.Finalidade = p.Finalidade;
                zap.DeclaracaoFinalidade = true;
            }

            if (servico.Id == "atpve")
            {
                if (string.IsNullOrWhiteSpace(p.AceiteAtpveVersao))
                    throw new DomainException("Leia o aviso e confirme a declaração do ATPV-e.");
                zap.AceiteAtpveVersao = p.AceiteAtpveVersao.Trim();
            }

            return (zap, identificador, placa);
        }

        /// <summary>CPF so com os 3 do meio e os 2 finais visiveis; CNPJ (empresa) inteiro.</summary>
        public static string MascararDocumento(string doc) => doc.Length == 11
            ? $"***.{doc.Substring(3, 3)}.{doc.Substring(6, 3)}-**"
            : $"{doc[..2]}.{doc.Substring(2, 3)}.{doc.Substring(5, 3)}/{doc.Substring(8, 4)}-{doc.Substring(12, 2)}";

        public async Task<ConsultaItem?> ObterAsync(int tenantId, int id, CancellationToken ct = default)
        {
            var consulta = await _db.ConsultasVeiculares.FirstOrDefaultAsync(c => c.CvcId == id, ct);
            if (consulta == null) return null;
            if (consulta.CvcStatus == ConsultaVeicular.Processando) await AtualizarAsync(tenantId, consulta, ct);
            return (await ItensAsync(new[] { consulta }, ct))[0];
        }

        public async Task<(byte[] Conteudo, string Tipo)?> DocumentoAsync(int id, CancellationToken ct = default)
        {
            var consulta = await _db.ConsultasVeiculares.AsNoTracking().FirstOrDefaultAsync(c => c.CvcId == id, ct);
            if (consulta?.CvcExternoId == null || consulta.CvcStatus != ConsultaVeicular.Concluida) return null;
            return await _zapcar.DocumentoAsync(consulta.CvcExternoId, ct);
        }

        public Task<AvisoAtpve?> AvisoAtpveAsync(CancellationToken ct = default) => _zapcar.AvisoAtpveAsync(ct);

        private async Task AtualizarAsync(int tenantId, ConsultaVeicular consulta, CancellationToken ct)
        {
            ZapCarResultado r;
            try
            {
                r = await _zapcar.ObterAsync(consulta.CvcExternoId!, ct);
            }
            catch (Exception ex) when (ex is ZapCarException or HttpRequestException or TaskCanceledException)
            {
                // Fica processando; a proxima leitura tenta de novo.
                _logger.LogWarning(ex, "Nao consegui ler a consulta {Id} na ZapCar", consulta.CvcExternoId);
                return;
            }

            if (r.Status == "concluido")
            {
                consulta.Concluir(r.RespostaJson, r.TemDocumento);
                await _db.SaveChangesAsync(ct);
            }
            else if (r.Status == "erro")
            {
                var alvo = string.IsNullOrEmpty(consulta.CvcPlaca) ? consulta.CvcIdentificador : consulta.CvcPlaca;
                var devolvido = await _creditos.EstornarAsync(tenantId, consulta.CvcValor, consulta.CvcReferencia,
                    $"Devolução: consulta {alvo} sem resultado", ct);
                var mensagem = r.ErroCodigo == "QUERY_NOT_FOUND"
                    ? "Nada foi encontrado nas bases consultadas para os dados informados."
                    : ZapCarClient.MensagemParaLoja(r.ErroCodigo ?? "", r.Erro);
                consulta.Falhar(mensagem, estornada: true);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Consulta {Id} ({Servico}) terminou em erro {Codigo}; devolvido={Devolvido}",
                    consulta.CvcExternoId, consulta.CvcServico, r.ErroCodigo, devolvido);
            }
        }

        private async Task<List<ConsultaItem>> ItensAsync(IEnumerable<ConsultaVeicular> consultas, CancellationToken ct)
        {
            var lista = consultas.ToList();
            var ids = lista.Select(c => c.R_UsuId).Distinct().ToList();
            var nomes = await _db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.UsuId))
                .ToDictionaryAsync(u => u.UsuId, u => u.UsuNome, ct);
            var catalogo = _settings.Configurado
                ? (await _zapcar.ServicosAsync(ct)).ToDictionary(s => s.Id)
                : new Dictionary<string, ZapCarServico>();

            return lista.Select(c =>
            {
                var (veiculo, dados) = Blocos(c.CvcResultadoJson);
                catalogo.TryGetValue(c.CvcServico, out var s);
                return new ConsultaItem
                {
                    Id = c.CvcId,
                    Placa = c.CvcPlaca,
                    Identificador = c.CvcIdentificador,
                    Servico = c.CvcServico,
                    ServicoNome = s?.Nome ?? c.CvcServico,
                    Categoria = s?.Categoria ?? "CONSULTAS",
                    Status = c.CvcStatus,
                    Valor = c.CvcValor,
                    Erro = c.CvcErro,
                    Estornada = c.CvcEstornada,
                    Teste = c.CvcTeste,
                    TemDocumento = c.CvcTemDocumento,
                    Usuario = nomes.TryGetValue(c.R_UsuId, out var u) ? u : null,
                    // Gravado em UTC: o "Z" no JSON faz o navegador mostrar no horario local.
                    CriadoEm = DateTime.SpecifyKind(c.CvcCriadoEm, DateTimeKind.Utc),
                    ConcluidoEm = c.CvcConcluidoEm.HasValue ? DateTime.SpecifyKind(c.CvcConcluidoEm.Value, DateTimeKind.Utc) : null,
                    Veiculo = veiculo,
                    Dados = dados,
                    Alertas = veiculo.HasValue ? ResumoConsulta.Avaliar(veiculo.Value.GetRawText()) : new()
                };
            }).ToList();
        }

        /// <summary>
        /// Separa "veiculo" e "dados" da resposta guardada. As primeiras
        /// consultas guardavam so o bloco "veiculo".
        /// </summary>
        private static (JsonElement? Veiculo, JsonElement? Dados) Blocos(string? json)
        {
            if (string.IsNullOrEmpty(json)) return (null, null);
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return (null, null);
            if (!raiz.TryGetProperty("status", out _)) return (raiz.Clone(), null);

            JsonElement? veiculo = raiz.TryGetProperty("veiculo", out var v) && v.ValueKind == JsonValueKind.Object ? v.Clone() : null;
            JsonElement? dados = raiz.TryGetProperty("dados", out var d) && d.ValueKind == JsonValueKind.Object ? d.Clone() : null;
            return (veiculo, dados);
        }
    }
}
