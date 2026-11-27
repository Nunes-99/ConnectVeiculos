using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.ConsultaPlaca
{
    public class PrecoPlanoInfo
    {
        public int PlanoId { get; set; }
        public string Plano { get; set; } = "";
        public decimal Margem { get; set; }
        /// <summary>Preco que vale hoje (fixo ou custo + margem).</summary>
        public decimal? Preco { get; set; }
        /// <summary>Valor fixo definido para este plano (null = usa o de todos ou a margem).</summary>
        public decimal? Fixo { get; set; }
    }

    public class PrecoServicoInfo
    {
        public string Servico { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal? Custo { get; set; }
        /// <summary>CRLV: custo varia por UF (entao so vale custo + margem).</summary>
        public bool VariaPorUf { get; set; }
        /// <summary>Valor fixo para todos os planos (null = margem de cada plano).</summary>
        public decimal? FixoTodos { get; set; }
        public List<PrecoPlanoInfo> Planos { get; set; } = new();
    }

    /// <summary>
    /// Preco das consultas para a loja: valor fixo do servico no plano dela,
    /// senao o fixo para todos os planos, senao custo ZapCar + margem do plano
    /// (arredondado para cima em ,90). A plataforma nao quer lucrar muito com
    /// consulta: a margem e' baixa e menor nos planos maiores.
    /// </summary>
    public class PrecosConsultaService
    {
        private readonly MasterDbContext _master;
        private readonly ZapCarSettings _settings;

        public PrecosConsultaService(MasterDbContext master, IOptions<ZapCarSettings> settings)
        {
            _master = master;
            _settings = settings.Value;
        }

        /// <summary>Custo + margem, arredondado para cima no proximo ",90".</summary>
        public static decimal ComMargem(decimal custo, decimal margemPercentual)
        {
            var bruto = custo * (1 + margemPercentual / 100m);
            var arredondado = Math.Ceiling(bruto + 0.10m) - 0.10m;
            return Math.Max(arredondado, 0.90m);
        }

        /// <summary>Tabela de precos que vale para um plano (servico -> preco). Custo por UF fica de fora.</summary>
        public async Task<Func<ZapCarServico, string?, decimal?>> TabelaAsync(int? planoId, string? planoNome, CancellationToken ct = default)
        {
            var fixos = await _master.PrecosConsulta.AsNoTracking()
                .Where(p => p.PlaId == null || p.PlaId == planoId)
                .ToListAsync(ct);
            var margem = _settings.MargemDoPlano(planoNome);

            return (servico, uf) =>
            {
                if (servico.PorUf.Count > 0)
                {
                    var custoUf = servico.PorUf.FirstOrDefault(u => u.Uf == uf)?.Custo;
                    return custoUf.HasValue ? ComMargem(custoUf.Value, margem) : null;
                }
                var doPlano = fixos.FirstOrDefault(p => p.PcsServico == servico.Id && p.PlaId != null);
                if (doPlano != null) return doPlano.Preco;
                var todos = fixos.FirstOrDefault(p => p.PcsServico == servico.Id && p.PlaId == null);
                if (todos != null) return todos.Preco;
                return servico.Custo.HasValue ? ComMargem(servico.Custo.Value, margem) : null;
            };
        }

        public async Task<List<PrecoServicoInfo>> GradeAsync(IReadOnlyList<ZapCarServico> catalogo, CancellationToken ct = default)
        {
            var planos = await _master.Planos.AsNoTracking().Where(p => p.PlaAtivo).OrderBy(p => p.PlaOrdem).ToListAsync(ct);
            var fixos = await _master.PrecosConsulta.AsNoTracking().ToListAsync(ct);

            return catalogo.Select(s =>
            {
                var todos = fixos.FirstOrDefault(f => f.PcsServico == s.Id && f.PlaId == null)?.Preco;
                return new PrecoServicoInfo
                {
                    Servico = s.Id,
                    Nome = s.Nome,
                    Categoria = s.Categoria,
                    Custo = s.Custo,
                    VariaPorUf = s.PorUf.Count > 0,
                    FixoTodos = todos,
                    Planos = planos.Select(p =>
                    {
                        var margem = _settings.MargemDoPlano(p.PlaNome);
                        var fixo = fixos.FirstOrDefault(f => f.PcsServico == s.Id && f.PlaId == p.PlaId)?.Preco;
                        return new PrecoPlanoInfo
                        {
                            PlanoId = p.PlaId,
                            Plano = p.PlaNome,
                            Margem = margem,
                            Fixo = fixo,
                            Preco = fixo ?? todos ?? (s.Custo.HasValue ? ComMargem(s.Custo.Value, margem) : null)
                        };
                    }).ToList()
                };
            }).ToList();
        }

        /// <summary>Grava (ou apaga, com preco nulo) o valor fixo do servico no plano (null = todos).</summary>
        public async Task DefinirAsync(string servico, int? planoId, decimal? preco, CancellationToken ct = default)
        {
            var atual = await _master.PrecosConsulta.FirstOrDefaultAsync(p => p.PcsServico == servico && p.PlaId == planoId, ct);
            if (preco == null)
            {
                if (atual != null) _master.PrecosConsulta.Remove(atual);
            }
            else if (atual == null)
            {
                _master.PrecosConsulta.Add(new PrecoConsulta(servico, planoId, preco.Value));
            }
            else
            {
                atual.Alterar(preco.Value);
            }
            await _master.SaveChangesAsync(ct);
        }
    }
}
