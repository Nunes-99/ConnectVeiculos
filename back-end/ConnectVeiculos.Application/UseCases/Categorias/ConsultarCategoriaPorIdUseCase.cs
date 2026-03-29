using ConnectVeiculos.Application.Interfaces.Categorias;
using ConnectVeiculos.Application.ViewModels.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;

namespace ConnectVeiculos.Application.UseCases.Categorias
{
    public class ConsultarCategoriaPorIdUseCase : IConsultarCategoriaPorIdUseCase
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public ConsultarCategoriaPorIdUseCase(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository;
        }

        public async Task<CategoriaViewModel> Execute(int id)
        {
            var categoria = await _categoriaRepository.GetByIdAsync(id);

            if (categoria == null)
                return null;

            return new CategoriaViewModel
            {
                CatId = categoria.CatId,
                CatNome = categoria.CatNome,
                CatDesc = categoria.CatDesc,
                CatSts = categoria.CatSts
            };
        }
    }
}
