using ConnectVeiculos.Application.ViewModels.Dashboard;

namespace ConnectVeiculos.Application.Interfaces.Dashboard
{
    public interface IConsultarDashboardUseCase
    {
        Task<DashboardViewModel> Execute();
    }
}
