using ConnectVeiculos.Application.InputModels.Categorias;
using ConnectVeiculos.Application.Interfaces.Categorias;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;

namespace ConnectVeiculos.Application.UseCases.Categorias
{
    public class CadastrarCategoriaUseCase : ICadastrarCategoriaUseCase
    {
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CadastrarCategoriaUseCase(ICategoriaRepository categoriaRepository, IUnitOfWork unitOfWork)
        {
            _categoriaRepository = categoriaRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Execute(CategoriaInputModel inputModel)
        {
            var categoria = new Categoria(
                inputModel.CatId,
                inputModel.CatNome,
                inputModel.CatDesc,
                inputModel.CatSts
            );

            _unitOfWork.BeginTransaction();

            try
            {
                var id = await _categoriaRepository.CreateAsync(categoria);
                _unitOfWork.Commit();
                return id;
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
