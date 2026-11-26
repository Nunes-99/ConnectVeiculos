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
    public class ServicoConsulta
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        public decimal Preco { get; set; }
        public bool Recomendado { get; set; }
    }

    public class ConsultaItem
    {
        public int Id { get; set; }
        public string Placa { get; set; } = "";
        public string Servico { get; set; } = "";
        public string ServicoNome { get; set; } = "";
        /// <summary>PROCESSANDO, CONCLUIDA ou ERRO.</summary>
        public string Status { get; set; } = "";
        public decimal Valor { get; set; }
        public string? Erro { get; set; }
        public bool Estornada { get; set; }
        public bool Teste { get; set; }
        public string? Usuario { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime? ConcluidoEm { get; set; }
        /// <summary>Bloco "veiculo" da ZapCar (snake_case), so quando concluida.</summary>
        public JsonElement? Veiculo { get; set; }
        public List<AlertaConsulta> Alertas { get; set; } = new();
        public bool NadaConsta => Status == ConsultaVeicular.Concluida && Alertas.Count == 0;
    }

    public class ConsultaPlacaInfo
    {
        public bool Disponivel { get; set; }
        /// <summary>Chave de teste: placas TST0001..TST0005, dados ficticios.</summary>
        public bool Teste { get; set; }
        public decimal Saldo { get; set; }
        public List<ServicoConsulta> Servicos { get; set; } = new();
        public IReadOnlyList<decimal> Pacotes { get; set; } = Array.Empty<decimal>();
        public List<ConsultaItem> Consultas { get; set; } = new();
        public IReadOnlyList<MovimentoCredito> Extrato { get; set; } = Array.Empty<MovimentoCredito>();
    }

    /// <summary>
    /// Consulta por placa: debita os creditos da loja, pede a ZapCar e guarda o
    /// resultado no banco da loja. Se a ZapCar recusar ou a consulta terminar em
    /// erro, o valor volta para os creditos (a ZapCar tambem nos devolve).
    /// </summary>
    public class ConsultaPlacaService
    {
        public const string Completa = "consulta-completa";
        public const string Simples = "consulta";
        private static readonly Regex FormatoPlaca = new("^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$", RegexOptions.Compiled);

        private readonly ConnectVeiculosDbContext _db;
        private readonly ICreditosConsultaService _creditos;
        private readonly IZapCarClient _zapcar;
        private readonly ZapCarSettings _settings;
        private readonly ILogger<ConsultaPlacaService> _logger;

        public ConsultaPlacaService(ConnectVeiculosDbContext db, ICreditosConsultaService creditos, IZapCarClient zapcar,
            IOptions<ZapCarSettings> settings, ILogger<ConsultaPlacaService> logger)
        {
            _db = db;
            _creditos = creditos;
            _zapcar = zapcar;
            _settings = settings.Value;
            _logger = logger;
        }

        public List<ServicoConsulta> Servicos() => new()
        {
            new()
            {
                Id = Completa, Nome = "Compra segura (completa)", Preco = _settings.PrecoCompleta, Recomendado = true,
                Descricao = "Gravame com o banco, leilão, sinistro, roubo e furto, restrições judiciais, débitos e dados do carro. Para decidir a compra ou a troca."
            },
            new()
            {
                Id = Simples, Nome = "Simples", Preco = _settings.PrecoSimples,
                Descricao = "Dados do carro, gravame, RENAJUD, roubo e furto e se foi baixado. Não verifica leilão nem sinistro."
            }
        };

        public static string NormalizarPlaca(string? placa)
            => new string((placa ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static bool PlacaValida(string placa) => FormatoPlaca.IsMatch(placa);

        public async Task<ConsultaPlacaInfo> PainelAsync(int tenantId, CancellationToken ct = default)
        {
            // Atualiza as que ficaram processando (ex.: a tela foi fechada no meio).
            var pendentes = await _db.ConsultasVeiculares
                .Where(c => c.CvcStatus == ConsultaVeicular.Processando && c.CvcExternoId != null)
                .OrderByDescending(c => c.CvcId).Take(5).ToListAsync(ct);
            foreach (var p in pendentes) await AtualizarAsync(tenantId, p, ct);

            var consultas = await _db.ConsultasVeiculares.AsNoTracking()
                .OrderByDescending(c => c.CvcId).Take(50).ToListAsync(ct);

            return new ConsultaPlacaInfo
            {
                Disponivel = _settings.Configurado,
                Teste = _settings.Teste,
                Saldo = await _creditos.SaldoAsync(tenantId, ct),
                Servicos = Servicos(),
                Pacotes = _settings.ValoresPacotes(),
                Consultas = await ItensAsync(consultas, ct),
                Extrato = await _creditos.ExtratoAsync(tenantId, 20, ct)
            };
        }

        public async Task<ConsultaItem> ConsultarAsync(int tenantId, string placaInformada, string servico, int usuarioId, CancellationToken ct = default)
        {
            if (!_settings.Configurado)
                throw new DomainException("A consulta por placa ainda não está disponível.");

            var placa = NormalizarPlaca(placaInformada);
            if (!PlacaValida(placa))
                throw new DomainException("Placa inválida. Use o formato ABC1234 ou ABC1D23.");

            var opcao = Servicos().FirstOrDefault(s => s.Id == servico)
                ?? throw new DomainException("Escolha o tipo de consulta.");

            var referencia = Guid.NewGuid().ToString("N");
            var descricao = $"Consulta {opcao.Nome.ToLowerInvariant()} {placa}";
            if (!await _creditos.DebitarAsync(tenantId, opcao.Preco, referencia, descricao, ct))
                throw new DomainException($"Créditos insuficientes: a consulta custa {opcao.Preco.ToString("C", new System.Globalization.CultureInfo("pt-BR"))}. Compre créditos para continuar.");

            var consulta = new ConsultaVeicular(placa, opcao.Id, referencia, opcao.Preco, usuarioId);
            try
            {
                var criada = await _zapcar.CriarAsync(opcao.Id, placa, referencia, ct);
                consulta.Enviada(criada.Id, criada.ValorCobrado, criada.Teste);
            }
            catch (Exception ex) when (ex is ZapCarException or HttpRequestException or TaskCanceledException)
            {
                await _creditos.EstornarAsync(tenantId, opcao.Preco, referencia, $"Devolução: {descricao}", CancellationToken.None);
                var mensagem = ex is ZapCarException z ? z.Message : "A base de consulta não respondeu. Tente de novo em alguns minutos.";
                consulta.Falhar(mensagem, estornada: true);
                _db.ConsultasVeiculares.Add(consulta);
                await _db.SaveChangesAsync(CancellationToken.None);
                _logger.LogWarning(ex, "Consulta {Placa} ({Servico}) recusada; creditos devolvidos", placa, opcao.Id);
                throw new DomainException(mensagem + " Nada foi cobrado.");
            }

            _db.ConsultasVeiculares.Add(consulta);
            await _db.SaveChangesAsync(ct);
            return (await ItensAsync(new[] { consulta }, ct))[0];
        }

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
                consulta.Concluir(r.VeiculoJson);
                await _db.SaveChangesAsync(ct);
            }
            else if (r.Status == "erro")
            {
                var devolvido = await _creditos.EstornarAsync(tenantId, consulta.CvcValor, consulta.CvcReferencia,
                    $"Devolução: consulta {consulta.CvcPlaca} sem resultado", ct);
                var mensagem = r.ErroCodigo == "QUERY_NOT_FOUND"
                    ? "A placa não foi encontrada nas bases consultadas."
                    : ZapCarClient.MensagemParaLoja(r.ErroCodigo ?? "", r.Erro);
                consulta.Falhar(mensagem, estornada: true);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Consulta {Id} ({Placa}) terminou em erro {Codigo}; devolvido={Devolvido}",
                    consulta.CvcExternoId, consulta.CvcPlaca, r.ErroCodigo, devolvido);
            }
        }

        private async Task<List<ConsultaItem>> ItensAsync(IEnumerable<ConsultaVeicular> consultas, CancellationToken ct)
        {
            var lista = consultas.ToList();
            var ids = lista.Select(c => c.R_UsuId).Distinct().ToList();
            var nomes = await _db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.UsuId))
                .ToDictionaryAsync(u => u.UsuId, u => u.UsuNome, ct);
            var servicos = Servicos().ToDictionary(s => s.Id, s => s.Nome);

            return lista.Select(c =>
            {
                JsonElement? veiculo = null;
                if (!string.IsNullOrEmpty(c.CvcResultadoJson))
                {
                    using var doc = JsonDocument.Parse(c.CvcResultadoJson);
                    veiculo = doc.RootElement.Clone();
                }
                return new ConsultaItem
                {
                    Id = c.CvcId,
                    Placa = c.CvcPlaca,
                    Servico = c.CvcServico,
                    ServicoNome = servicos.TryGetValue(c.CvcServico, out var n) ? n : c.CvcServico,
                    Status = c.CvcStatus,
                    Valor = c.CvcValor,
                    Erro = c.CvcErro,
                    Estornada = c.CvcEstornada,
                    Teste = c.CvcTeste,
                    Usuario = nomes.TryGetValue(c.R_UsuId, out var u) ? u : null,
                    CriadoEm = c.CvcCriadoEm,
                    ConcluidoEm = c.CvcConcluidoEm,
                    Veiculo = veiculo,
                    Alertas = ResumoConsulta.Avaliar(c.CvcResultadoJson)
                };
            }).ToList();
        }
    }
}
