using ConnectVeiculos.Application.ViewModels.Vendas;

namespace ConnectVeiculos.Application.Interfaces.Vendas
{
    public interface IConsultarVendaPorIdUseCase
    {
        Task<VendaViewModel> Execute(int id);
    }
}
