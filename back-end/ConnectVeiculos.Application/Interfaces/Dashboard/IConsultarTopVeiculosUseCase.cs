using ConnectVeiculos.Application.ViewModels.Dashboard;

namespace ConnectVeiculos.Application.Interfaces.Dashboard
{
    public interface IConsultarTopVeiculosUseCase
    {
        Task<TopVeiculosVendidosViewModel> Execute(int quantidade = 10);
    }
}
