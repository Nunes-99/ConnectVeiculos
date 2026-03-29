using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Categorias;

namespace ConnectVeiculos.Application.Interfaces.Categorias
{
    public interface IConsultarCategoriasPaginadoUseCase
    {
        Task<PagedResult<CategoriaViewModel>> Execute(int page, int pageSize, string? search = null);
    }
}
