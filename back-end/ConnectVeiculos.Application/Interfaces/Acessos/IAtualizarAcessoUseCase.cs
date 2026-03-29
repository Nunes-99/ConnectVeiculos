using ConnectVeiculos.Application.InputModels.Acessos;

namespace ConnectVeiculos.Application.Interfaces.Acessos
{
    public interface IAtualizarAcessoUseCase
    {
        Task Execute(AcessoInputModel inputModel);
    }
}
