using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Application.ViewModels.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;

namespace ConnectVeiculos.Application.UseCases.Lojas
{
    public class ConsultarLojaPorIdUseCase : IConsultarLojaPorIdUseCase
    {
        private readonly ILojaRepository _lojaRepository;

        public ConsultarLojaPorIdUseCase(ILojaRepository lojaRepository)
        {
            _lojaRepository = lojaRepository;
        }

        public async Task<LojaViewModel> Execute(int id)
        {
            var loja = await _lojaRepository.GetByIdAsync(id);

            if (loja == null)
                return null;

            return new LojaViewModel
            {
                LojId = loja.LojId,
                LojNome = loja.LojNome,
                LojLogradouro = loja.LojLogradouro,
                LojNumero = loja.LojNumero,
                LojBairro = loja.LojBairro,
                LojCidade = loja.LojCidade,
                LojEstado = loja.LojEstado,
                LojCEP = loja.LojCEP,
                LojComplemento = loja.LojComplemento,
                LojEmail = loja.LojEmail,
                LojTel1 = loja.LojTel1,
                LojTel2 = loja.LojTel2,
                LojWhatsApp = loja.LojWhatsApp,
                LojImg = loja.LojImg,
                LojCNPJ = loja.LojCNPJ,
                LojIE = loja.LojIE,
                LojSts = loja.LojSts,
                LojCorPrimaria = loja.LojCorPrimaria,
                LojTema = loja.LojTema,
                LojCorFundo = loja.LojCorFundo,
                LojBannerImg = loja.LojBannerImg,
                LojBannerTitulo = loja.LojBannerTitulo,
                LojBannerSubtitulo = loja.LojBannerSubtitulo,
                LojFavicon = loja.LojFavicon,
                LojHorario = loja.LojHorario,
                LojSobre = loja.LojSobre,
                LojLinkVenderCarro = loja.LojLinkVenderCarro,
                LojMostrarMarcas = loja.LojMostrarMarcas,
                LojMostrarMapa = loja.LojMostrarMapa,
                LojCorSecundaria = loja.LojCorSecundaria,
                LojInstagram = loja.LojInstagram,
                LojFacebook = loja.LojFacebook,
                LojSlug = loja.LojSlug,
                LojUrlCatalogo = loja.LojUrlCatalogo,
                LojPadraoCatalogo = loja.LojPadraoCatalogo
            };
        }
    }
}
