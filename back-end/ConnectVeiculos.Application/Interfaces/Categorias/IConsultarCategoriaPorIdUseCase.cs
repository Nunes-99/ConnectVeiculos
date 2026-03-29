using ConnectVeiculos.Application.ViewModels.Categorias;

namespace ConnectVeiculos.Application.Interfaces.Categorias
{
    public interface IConsultarCategoriaPorIdUseCase
    {
        Task<CategoriaViewModel> Execute(int id);
    }
}
