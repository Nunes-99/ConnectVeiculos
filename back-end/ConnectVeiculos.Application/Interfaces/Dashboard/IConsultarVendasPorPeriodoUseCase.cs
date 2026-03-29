using ConnectVeiculos.Application.ViewModels.Dashboard;

namespace ConnectVeiculos.Application.Interfaces.Dashboard
{
    public interface IConsultarVendasPorPeriodoUseCase
    {
        Task<VendasPorPeriodoViewModel> Execute(DateTime dataInicio, DateTime dataFim);
    }
}
