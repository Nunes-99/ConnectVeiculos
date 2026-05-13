using ConnectVeiculos.Application.Interfaces.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;

namespace ConnectVeiculos.Application.UseCases.Categorias
{
    public class InativarCategoriaUseCase : IInativarCategoriaUseCase
    {
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public InativarCategoriaUseCase(ICategoriaRepository categoriaRepository, IUnitOfWork unitOfWork)
        {
            _categoriaRepository = categoriaRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(int id)
        {
            var categoria = await _categoriaRepository.GetByIdAsync(id);

            if (categoria == null)
                throw new Exception("Categoria não encontrada.");

            _unitOfWork.BeginTransaction();

            try
            {
                await _categoriaRepository.DeleteAsync(id);
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
