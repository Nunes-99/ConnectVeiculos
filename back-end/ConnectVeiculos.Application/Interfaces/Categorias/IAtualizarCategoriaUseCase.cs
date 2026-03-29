using ConnectVeiculos.Application.InputModels.Categorias;

namespace ConnectVeiculos.Application.Interfaces.Categorias
{
    public interface IAtualizarCategoriaUseCase
    {
        Task Execute(CategoriaInputModel inputModel);
    }
}
