using ConnectVeiculos.Application.ViewModels.Dashboard;

namespace ConnectVeiculos.Application.Interfaces.Dashboard
{
    public interface IConsultarLucroDashboardUseCase
    {
        Task<LucroDashboardViewModel> Execute(DateTime? dataInicio = null, DateTime? dataFim = null, int? lojaId = null);
    }
}
