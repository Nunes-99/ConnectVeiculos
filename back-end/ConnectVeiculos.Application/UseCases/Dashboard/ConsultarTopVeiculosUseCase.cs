using ConnectVeiculos.Application.Interfaces.Dashboard;
using ConnectVeiculos.Application.ViewModels.Dashboard;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;

namespace ConnectVeiculos.Application.UseCases.Dashboard
{
    public class ConsultarTopVeiculosUseCase : IConsultarTopVeiculosUseCase
    {
        private readonly IVendaRepository _vendaRepository;

        public ConsultarTopVeiculosUseCase(IVendaRepository vendaRepository)
        {
            _vendaRepository = vendaRepository;
        }

        public async Task<TopVeiculosVendidosViewModel> Execute(int quantidade = 10)
        {
            var todasVendas = await _vendaRepository.GetAllAsync();

            var vendasAtivas = todasVendas.Where(v => v.VenStatus == "A").ToList();

            var topVeiculos = vendasAtivas
                .GroupBy(v => new { v.VenMarca, v.VenModelo })
                .Select(g => new VeiculoVendidoViewModel
                {
                    Marca = g.Key.VenMarca ?? "",
                    Modelo = g.Key.VenModelo ?? "",
                    QuantidadeVendida = g.Count(),
                    ValorTotalVendas = g.Sum(v => v.VenValor),
                    TicketMedio = g.Count() > 0 ? g.Sum(v => v.VenValor) / g.Count() : 0
                })
                .OrderByDescending(v => v.QuantidadeVendida)
                .ThenByDescending(v => v.ValorTotalVendas)
                .Take(quantidade)
                .ToList();

            return new TopVeiculosVendidosViewModel
            {
                Veiculos = topVeiculos
            };
        }
    }
}
