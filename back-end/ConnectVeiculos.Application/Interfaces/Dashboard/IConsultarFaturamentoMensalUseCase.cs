using ConnectVeiculos.Application.ViewModels.Dashboard;

namespace ConnectVeiculos.Application.Interfaces.Dashboard
{
    public interface IConsultarFaturamentoMensalUseCase
    {
        Task<FaturamentoMensalViewModel> Execute(int ano);
    }
}
