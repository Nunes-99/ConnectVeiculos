using ConnectVeiculos.Application.Interfaces.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;

namespace ConnectVeiculos.Application.UseCases.Acessos
{
    public class InativarAcessoUseCase : IInativarAcessoUseCase
    {
        private readonly IAcessoRepository _acessoRepository;
        private readonly IUnitOfWork _unitOfWork;

        public InativarAcessoUseCase(IAcessoRepository acessoRepository, IUnitOfWork unitOfWork)
        {
            _acessoRepository = acessoRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(int id)
        {
            var acesso = await _acessoRepository.GetByIdAsync(id);

            if (acesso == null)
                throw new Exception("Acesso nao encontrado.");

            _unitOfWork.BeginTransaction();

            try
            {
                await _acessoRepository.DeleteAsync(id);
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
