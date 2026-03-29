using ConnectVeiculos.Application.InputModels.Acessos;
using ConnectVeiculos.Application.Interfaces.Acessos;
using ConnectVeiculos.Core.Entities.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;

namespace ConnectVeiculos.Application.UseCases.Acessos
{
    public class CadastrarAcessoUseCase : ICadastrarAcessoUseCase
    {
        private readonly IAcessoRepository _acessoRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CadastrarAcessoUseCase(IAcessoRepository acessoRepository, IUnitOfWork unitOfWork)
        {
            _acessoRepository = acessoRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Execute(AcessoInputModel inputModel)
        {
            var acesso = new Acesso(
                inputModel.AcsId,
                inputModel.AcsNome,
                inputModel.AcsDesc,
                inputModel.AcsSts
            );

            _unitOfWork.BeginTransaction();

            try
            {
                var id = await _acessoRepository.CreateAsync(acesso);
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
