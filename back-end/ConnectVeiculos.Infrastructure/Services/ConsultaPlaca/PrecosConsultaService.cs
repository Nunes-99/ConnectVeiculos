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
        /// <summary>Acrescimo (R$) do plano sobre o custo.</summary>
        public decimal Acrescimo { get; set; }
        /// <summary>Preco que vale hoje (fixo ou custo + acrescimo).</summary>
        public decimal? Preco { get; set; }
        /// <summary>Valor fixo definido para este plano (null = usa o de todos ou o acrescimo).</summary>
        public decimal? Fixo { get; set; }
    }

    public class PrecoServicoInfo
    {
        public string Servico { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal? Custo { get; set; }
        /// <summary>CRLV: custo varia por UF (entao so vale custo + acrescimo).</summary>
        public bool VariaPorUf { get; set; }
        /// <summary>Valor fixo para todos os planos (null = acrescimo de cada plano).</summary>
        public decimal? FixoTodos { get; set; }
        public List<PrecoPlanoInfo> Planos { get; set; } = new();
    }

    /// <summary>
    /// Preco das consultas para a loja: valor fixo do servico no plano dela,
    /// senao o fixo para todos os planos, senao custo ZapCar + acrescimo fixo em
    /// R$ do plano. A plataforma nao quer lucrar muito com consulta: o
    /// acrescimo e' baixo e menor nos planos maiores.
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

        /// <summary>Custo + acrescimo fixo do plano, em reais.</summary>
        public static decimal ComAcrescimo(decimal custo, decimal acrescimo) =>
            Math.Round(custo + acrescimo, 2, MidpointRounding.AwayFromZero);

        /// <summary>Tabela de precos que vale para um plano (servico -> preco). Custo por UF fica de fora.</summary>
        public async Task<Func<ZapCarServico, string?, decimal?>> TabelaAsync(int? planoId, string? planoNome, CancellationToken ct = default)
        {
            var fixos = await _master.PrecosConsulta.AsNoTracking()
                .Where(p => p.PlaId == null || p.PlaId == planoId)
                .ToListAsync(ct);
            var acrescimo = _settings.AcrescimoDoPlano(planoNome);

            return (servico, uf) =>
            {
                if (servico.PorUf.Count > 0)
                {
                    var custoUf = servico.PorUf.FirstOrDefault(u => u.Uf == uf)?.Custo;
                    return custoUf.HasValue ? ComAcrescimo(custoUf.Value, acrescimo) : null;
                }
                var doPlano = fixos.FirstOrDefault(p => p.PcsServico == servico.Id && p.PlaId != null);
                if (doPlano != null) return doPlano.Preco;
                var todos = fixos.FirstOrDefault(p => p.PcsServico == servico.Id && p.PlaId == null);
                if (todos != null) return todos.Preco;
                return servico.Custo.HasValue ? ComAcrescimo(servico.Custo.Value, acrescimo) : null;
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
                        var acrescimo = _settings.AcrescimoDoPlano(p.PlaNome);
                        var fixo = fixos.FirstOrDefault(f => f.PcsServico == s.Id && f.PlaId == p.PlaId)?.Preco;
                        return new PrecoPlanoInfo
                        {
                            PlanoId = p.PlaId,
                            Plano = p.PlaNome,
                            Acrescimo = acrescimo,
                            Fixo = fixo,
                            Preco = fixo ?? todos ?? (s.Custo.HasValue ? ComAcrescimo(s.Custo.Value, acrescimo) : null)
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
