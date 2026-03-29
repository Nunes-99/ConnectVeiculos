using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;

namespace ConnectVeiculos.Application.UseCases.Lojas
{
    public class InativarLojaUseCase : IInativarLojaUseCase
    {
        private readonly ILojaRepository _lojaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public InativarLojaUseCase(ILojaRepository lojaRepository, IUnitOfWork unitOfWork)
        {
            _lojaRepository = lojaRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(int id)
        {
            var loja = await _lojaRepository.GetByIdAsync(id);

            if (loja == null)
                throw new Exception("Loja nao encontrada.");

            loja.AlterarStatus(false);

            _unitOfWork.BeginTransaction();

            try
            {
                await _lojaRepository.UpdateAsync(loja);
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
