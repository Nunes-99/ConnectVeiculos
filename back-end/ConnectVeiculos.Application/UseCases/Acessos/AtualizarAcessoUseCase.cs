using ConnectVeiculos.Application.InputModels.Acessos;
using ConnectVeiculos.Application.Interfaces.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;

namespace ConnectVeiculos.Application.UseCases.Acessos
{
    public class AtualizarAcessoUseCase : IAtualizarAcessoUseCase
    {
        private readonly IAcessoRepository _acessoRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AtualizarAcessoUseCase(IAcessoRepository acessoRepository, IUnitOfWork unitOfWork)
        {
            _acessoRepository = acessoRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(AcessoInputModel inputModel)
        {
            var acesso = await _acessoRepository.GetByIdAsync(inputModel.AcsId);

            if (acesso == null)
                throw new Exception("Acesso não encontrado.");

            acesso.SetProperties(
                inputModel.AcsId,
                inputModel.AcsNome,
                inputModel.AcsDesc,
                inputModel.AcsSts
            );

            _unitOfWork.BeginTransaction();

            try
            {
                await _acessoRepository.UpdateAsync(acesso);
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
