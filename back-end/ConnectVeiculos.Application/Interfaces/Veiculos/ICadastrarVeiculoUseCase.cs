using ConnectVeiculos.Application.InputModels.Veiculos;

namespace ConnectVeiculos.Application.Interfaces.Veiculos
{
    public interface ICadastrarVeiculoUseCase
    {
        Task<int> Execute(VeiculoInputModel inputModel);
    }
}
