using ConnectVeiculos.Application.Interfaces.Categorias;
using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;

namespace ConnectVeiculos.Application.UseCases.Categorias
{
    public class ConsultarCategoriasPaginadoUseCase : IConsultarCategoriasPaginadoUseCase
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public ConsultarCategoriasPaginadoUseCase(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository;
        }

        public async Task<PagedResult<CategoriaViewModel>> Execute(int page, int pageSize, string? search = null)
        {
            var (items, total) = await _categoriaRepository.GetPagedAsync(page, pageSize, search);

            var viewModels = items.Select(c => new CategoriaViewModel
            {
                CatId = c.CatId,
                CatNome = c.CatNome,
                CatDesc = c.CatDesc ?? "",
                CatSts = c.CatSts
            }).ToList();

            return new PagedResult<CategoriaViewModel>(viewModels, total, page, pageSize);
        }
    }
}
