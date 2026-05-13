using ConnectVeiculos.Application.InputModels.Categorias;
using ConnectVeiculos.Application.Interfaces.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;

namespace ConnectVeiculos.Application.UseCases.Categorias
{
    public class AtualizarCategoriaUseCase : IAtualizarCategoriaUseCase
    {
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AtualizarCategoriaUseCase(ICategoriaRepository categoriaRepository, IUnitOfWork unitOfWork)
        {
            _categoriaRepository = categoriaRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(CategoriaInputModel inputModel)
        {
            var categoria = await _categoriaRepository.GetByIdAsync(inputModel.CatId);

            if (categoria == null)
                throw new Exception("Categoria não encontrada.");

            categoria.SetProperties(
                inputModel.CatId,
                inputModel.CatNome,
                inputModel.CatDesc,
                inputModel.CatSts
            );

            _unitOfWork.BeginTransaction();

            try
            {
                await _categoriaRepository.UpdateAsync(categoria);
                _unitOfWork.Commit();
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
