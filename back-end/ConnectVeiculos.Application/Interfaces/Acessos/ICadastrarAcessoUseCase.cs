using ConnectVeiculos.Application.InputModels.Acessos;

namespace ConnectVeiculos.Application.Interfaces.Acessos
{
    public interface ICadastrarAcessoUseCase
    {
        Task<int> Execute(AcessoInputModel inputModel);
    }
}
