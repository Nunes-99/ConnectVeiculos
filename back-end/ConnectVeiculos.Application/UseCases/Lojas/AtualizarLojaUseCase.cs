using ConnectVeiculos.Application.InputModels.Lojas;
using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;

namespace ConnectVeiculos.Application.UseCases.Lojas
{
    public class AtualizarLojaUseCase : IAtualizarLojaUseCase
    {
        private readonly ILojaRepository _lojaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AtualizarLojaUseCase(ILojaRepository lojaRepository, IUnitOfWork unitOfWork)
        {
            _lojaRepository = lojaRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(LojaInputModel inputModel)
        {
            var loja = await _lojaRepository.GetByIdAsync(inputModel.LojId);

            if (loja == null)
                throw new Exception("Loja nao encontrada.");

            loja.SetProperties(
                inputModel.LojId,
                inputModel.LojNome,
                inputModel.LojLogradouro,
                inputModel.LojNumero,
                inputModel.LojBairro,
                inputModel.LojCidade,
                inputModel.LojEstado,
                inputModel.LojCEP,
                inputModel.LojComplemento,
                inputModel.LojEmail,
                inputModel.LojTel1,
                inputModel.LojTel2,
                inputModel.LojWhatsApp,
                inputModel.LojImg,
                inputModel.LojCNPJ,
                inputModel.LojIE,
                inputModel.LojSts,
                inputModel.LojCorPrimaria,
                inputModel.LojCorSecundaria,
                inputModel.LojInstagram,
                inputModel.LojFacebook,
                inputModel.LojSlug,
                inputModel.LojUrlCatalogo
            );

            _unitOfWork.BeginTransaction();

            try
            {
                await _lojaRepository.UpdateAsync(loja);

                // Replicar URL do catálogo para todas as lojas
                if (!string.IsNullOrWhiteSpace(inputModel.LojUrlCatalogo))
                {
                    var todasLojas = await _lojaRepository.GetAllAsync();
                    foreach (var outraLoja in todasLojas.Where(l => l.LojId != inputModel.LojId))
                    {
                        outraLoja.SetUrlCatalogo(inputModel.LojUrlCatalogo);
                        await _lojaRepository.UpdateAsync(outraLoja);
                    }
                }

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
