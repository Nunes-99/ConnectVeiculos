using ConnectVeiculos.Application.ViewModels.Vendas;

namespace ConnectVeiculos.Application.Interfaces.Vendas
{
    public interface IConsultarVendasUseCase
    {
        Task<IEnumerable<VendaViewModel>> Execute();
    }
}
