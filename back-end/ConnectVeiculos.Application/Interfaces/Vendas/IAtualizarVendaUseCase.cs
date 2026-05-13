using ConnectVeiculos.Application.InputModels.Vendas;

namespace ConnectVeiculos.Application.Interfaces.Vendas
{
    public interface IAtualizarVendaUseCase
    {
        Task Execute(int vendaId, VendaInputModel inputModel);
    }
}
