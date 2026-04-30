using ConnectVeiculos.Application.Interfaces.Dashboard;
using ConnectVeiculos.Application.ViewModels.Dashboard;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Despesas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;

namespace ConnectVeiculos.Application.UseCases.Dashboard
{
    public class ConsultarLucroDashboardUseCase : IConsultarLucroDashboardUseCase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoDespesaRepository _despesaRepository;

        public ConsultarLucroDashboardUseCase(
            IVendaRepository vendaRepository,
            IVeiculoRepository veiculoRepository,
            IVeiculoDespesaRepository despesaRepository)
        {
            _vendaRepository = vendaRepository;
            _veiculoRepository = veiculoRepository;
            _despesaRepository = despesaRepository;
        }

        public async Task<LucroDashboardViewModel> Execute(DateTime? dataInicio = null, DateTime? dataFim = null, int? lojaId = null)
        {
            var inicio = dataInicio ?? DateTime.Today.AddMonths(-12);
            var fim = (dataFim ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1);

            var vendas = (await _vendaRepository.GetAllAsync())
                .Where(v => v.VenStatus == "A" && v.VenDtVenda >= inicio && v.VenDtVenda <= fim)
                .ToList();

            var veiculos = (await _veiculoRepository.GetAllAsync()).ToDictionary(v => v.VeiId);
            var despesasPorVeiculo = (await _despesaRepository.GetAllAsync())
                .GroupBy(d => d.R_VeiId)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.DesValor));

            if (lojaId.HasValue)
                vendas = vendas.Where(v => veiculos.TryGetValue(v.R_VeiId, out var veiculo) && veiculo.R_LojId == lojaId.Value).ToList();

            decimal receita = vendas.Sum(v => v.VenValor);
            decimal custoCompra = vendas.Sum(v => veiculos.TryGetValue(v.R_VeiId, out var veiculo) ? veiculo.VeiPrecoCompra : 0);
            decimal totalDespesas = vendas.Sum(v => despesasPorVeiculo.TryGetValue(v.R_VeiId, out var d) ? d : 0);
            decimal totalComissoes = vendas.Sum(v => v.VenComissaoValor);
            decimal lucroLiquido = receita - custoCompra - totalDespesas - totalComissoes;

            var topVeiculos = vendas
                .Select(v =>
                {
                    veiculos.TryGetValue(v.R_VeiId, out var veiculo);
                    despesasPorVeiculo.TryGetValue(v.R_VeiId, out var despesa);
                    decimal lucro = v.VenValor - (veiculo?.VeiPrecoCompra ?? 0) - despesa - v.VenComissaoValor;
                    return new TopVeiculoRentavelViewModel
                    {
                        VeiId = v.R_VeiId,
                        Marca = veiculo?.VeiMarca ?? "",
                        Modelo = veiculo?.VeiModelo ?? "",
                        Ano = veiculo?.VeiAno ?? 0,
                        PrecoVenda = v.VenValor,
                        PrecoCompra = veiculo?.VeiPrecoCompra ?? 0,
                        Despesas = despesa,
                        Lucro = lucro,
                        Margem = v.VenValor > 0 ? Math.Round(lucro / v.VenValor * 100, 2) : 0
                    };
                })
                .OrderByDescending(t => t.Lucro)
                .Take(10)
                .ToList();

            var lucroPorMes = vendas
                .GroupBy(v => new { v.VenDtVenda.Year, v.VenDtVenda.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    decimal r = g.Sum(v => v.VenValor);
                    decimal c = g.Sum(v => veiculos.TryGetValue(v.R_VeiId, out var veiculo) ? veiculo.VeiPrecoCompra : 0);
                    decimal d = g.Sum(v => despesasPorVeiculo.TryGetValue(v.R_VeiId, out var dv) ? dv : 0);
                    decimal com = g.Sum(v => v.VenComissaoValor);
                    return new LucroPorMesViewModel
                    {
                        Periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                        Receita = r,
                        Lucro = r - c - d - com
                    };
                })
                .ToList();

            return new LucroDashboardViewModel
            {
                Receita = receita,
                CustoCompra = custoCompra,
                Despesas = totalDespesas,
                Comissoes = totalComissoes,
                LucroLiquido = lucroLiquido,
                MargemMedia = receita > 0 ? Math.Round(lucroLiquido / receita * 100, 2) : 0,
                TotalVendas = vendas.Count,
                LucroPorMes = lucroPorMes,
                TopVeiculosRentaveis = topVeiculos
            };
        }
    }
}
