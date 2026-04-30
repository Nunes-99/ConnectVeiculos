using ConnectVeiculos.Application.InputModels.Lojas;
using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;

namespace ConnectVeiculos.Application.UseCases.Lojas
{
    public class CadastrarLojaUseCase : ICadastrarLojaUseCase
    {
        private readonly ILojaRepository _lojaRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CadastrarLojaUseCase(ILojaRepository lojaRepository, IUnitOfWork unitOfWork)
        {
            _lojaRepository = lojaRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Execute(LojaInputModel inputModel)
        {
            var loja = new Loja(
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
                var id = await _lojaRepository.CreateAsync(loja);

                // Replicar URL do catálogo para todas as lojas
                if (!string.IsNullOrWhiteSpace(inputModel.LojUrlCatalogo))
                {
                    var todasLojas = await _lojaRepository.GetAllAsync();
                    foreach (var outraLoja in todasLojas.Where(l => l.LojId != id))
                    {
                        outraLoja.SetUrlCatalogo(inputModel.LojUrlCatalogo);
                        await _lojaRepository.UpdateAsync(outraLoja);
                    }
                }

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
