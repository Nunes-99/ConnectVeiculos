using ConnectVeiculos.Application.InputModels.Veiculos;

namespace ConnectVeiculos.Application.Interfaces.Veiculos
{
    public interface IAtualizarVeiculoUseCase
    {
        Task Execute(VeiculoInputModel inputModel);
    }
}
