using ConnectVeiculos.Application.Interfaces.Dashboard;
using ConnectVeiculos.Application.ViewModels.Dashboard;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;

namespace ConnectVeiculos.Application.UseCases.Dashboard
{
    public class ConsultarVendasPorPeriodoUseCase : IConsultarVendasPorPeriodoUseCase
    {
        private readonly IVendaRepository _vendaRepository;

        public ConsultarVendasPorPeriodoUseCase(IVendaRepository vendaRepository)
        {
            _vendaRepository = vendaRepository;
        }

        public async Task<VendasPorPeriodoViewModel> Execute(DateTime dataInicio, DateTime dataFim)
        {
            var todasVendas = await _vendaRepository.GetAllAsync();

            var vendasPeriodo = todasVendas
                .Where(v => v.VenStatus == "A" && v.VenDtVenda >= dataInicio && v.VenDtVenda <= dataFim)
                .ToList();

            var vendasAgrupadas = vendasPeriodo
                .GroupBy(v => v.VenDtVenda.Date)
                .OrderBy(g => g.Key)
                .Select(g => new VendaDiaViewModel
                {
                    Data = g.Key,
                    Quantidade = g.Count(),
                    Valor = g.Sum(v => v.VenValor)
                })
                .ToList();

            return new VendasPorPeriodoViewModel
            {
                Vendas = vendasAgrupadas,
                TotalPeriodo = vendasPeriodo.Sum(v => v.VenValor),
                QuantidadeVendas = vendasPeriodo.Count
            };
        }
    }
}
