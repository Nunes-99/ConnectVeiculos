using ConnectVeiculos.Application.ViewModels.Categorias;

namespace ConnectVeiculos.Application.Interfaces.Categorias
{
    public interface IConsultarCategoriasUseCase
    {
        Task<List<CategoriaViewModel>> Execute(string pesquisa, string inicio, string intervalo);
    }
}
