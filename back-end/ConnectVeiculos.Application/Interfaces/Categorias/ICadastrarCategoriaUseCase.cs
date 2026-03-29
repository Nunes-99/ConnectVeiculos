using ConnectVeiculos.Application.InputModels.Categorias;

namespace ConnectVeiculos.Application.Interfaces.Categorias
{
    public interface ICadastrarCategoriaUseCase
    {
        Task<int> Execute(CategoriaInputModel inputModel);
    }
}
