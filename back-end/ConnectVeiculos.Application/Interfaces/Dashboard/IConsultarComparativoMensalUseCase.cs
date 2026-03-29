using ConnectVeiculos.Application.ViewModels.Dashboard;

namespace ConnectVeiculos.Application.Interfaces.Dashboard
{
    public interface IConsultarComparativoMensalUseCase
    {
        Task<ComparativoMensalViewModel> Execute();
    }
}
