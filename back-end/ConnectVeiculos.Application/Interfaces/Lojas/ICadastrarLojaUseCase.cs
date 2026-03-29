using ConnectVeiculos.Application.InputModels.Lojas;

namespace ConnectVeiculos.Application.Interfaces.Lojas
{
    public interface ICadastrarLojaUseCase
    {
        Task<int> Execute(LojaInputModel inputModel);
    }
}
