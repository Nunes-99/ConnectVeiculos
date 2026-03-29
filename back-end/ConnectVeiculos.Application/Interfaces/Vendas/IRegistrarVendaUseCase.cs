using ConnectVeiculos.Application.InputModels.Vendas;

namespace ConnectVeiculos.Application.Interfaces.Vendas
{
    public interface IRegistrarVendaUseCase
    {
        Task<int> Execute(VendaInputModel inputModel);
    }
}
