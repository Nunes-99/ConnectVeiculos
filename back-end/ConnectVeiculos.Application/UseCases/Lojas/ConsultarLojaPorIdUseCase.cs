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
                LojCorSecundaria = loja.LojCorSecundaria,
                LojInstagram = loja.LojInstagram,
                LojFacebook = loja.LojFacebook,
                LojSlug = loja.LojSlug
            };
        }
    }
}
