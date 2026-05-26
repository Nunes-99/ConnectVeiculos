using ConnectVeiculos.Application.InputModels.Lojas;
using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Application.UseCases.Lojas
{
    public class CadastrarLojaUseCase : ICadastrarLojaUseCase
    {
        private readonly ILojaRepository _lojaRepository;
        private readonly IUnitOfWork _unitOfWork;
         private readonly ILimiteService _limiteService;

        public CadastrarLojaUseCase(ILojaRepository lojaRepository, IUnitOfWork unitOfWork, ILimiteService limiteService)
        {
            _lojaRepository = lojaRepository;
            _unitOfWork = unitOfWork;
             _limiteService = limiteService;
        }

        public async Task<int> Execute(LojaInputModel inputModel)
        {
             await _limiteService.GarantirPodeCriarLojaAsync();
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
                inputModel.LojUrlCatalogo,
                inputModel.LojPadraoCatalogo
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

                // Exclusividade da loja padrao do catalogo
                if (inputModel.LojPadraoCatalogo)
                {
                    var todasLojas = await _lojaRepository.GetAllAsync();
                    foreach (var outraLoja in todasLojas.Where(l => l.LojId != id && l.LojPadraoCatalogo))
                    {
                        outraLoja.DefinirComoPadraoCatalogo(false);
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
