using ConnectVeiculos.Application.InputModels.Lojas;

namespace ConnectVeiculos.Application.Interfaces.Lojas
{
    public interface IAtualizarLojaUseCase
    {
        Task Execute(LojaInputModel inputModel);
    }
}
