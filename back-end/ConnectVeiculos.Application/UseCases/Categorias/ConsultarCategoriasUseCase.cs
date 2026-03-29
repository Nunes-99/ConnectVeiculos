using ConnectVeiculos.Application.Interfaces.Categorias;
using ConnectVeiculos.Application.ViewModels.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;

namespace ConnectVeiculos.Application.UseCases.Categorias
{
    public class ConsultarCategoriasUseCase : IConsultarCategoriasUseCase
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public ConsultarCategoriasUseCase(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository;
        }

        public async Task<List<CategoriaViewModel>> Execute(string pesquisa, string inicio, string intervalo)
        {
            var categorias = await _categoriaRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(pesquisa))
            {
                categorias = categorias.Where(c =>
                    c.CatNome.Contains(pesquisa, StringComparison.OrdinalIgnoreCase) ||
                    (c.CatDesc != null && c.CatDesc.Contains(pesquisa, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return categorias.Select(c => new CategoriaViewModel
            {
                CatId = c.CatId,
                CatNome = c.CatNome,
                CatDesc = c.CatDesc,
                CatSts = c.CatSts
            }).ToList();
        }
    }
}
