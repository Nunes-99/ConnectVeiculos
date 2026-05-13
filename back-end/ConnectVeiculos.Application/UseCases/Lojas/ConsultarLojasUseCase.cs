using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Application.ViewModels.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;

namespace ConnectVeiculos.Application.UseCases.Lojas
{
    public class ConsultarLojasUseCase : IConsultarLojasUseCase
    {
        private readonly ILojaRepository _lojaRepository;

        public ConsultarLojasUseCase(ILojaRepository lojaRepository)
        {
            _lojaRepository = lojaRepository;
        }

        public async Task<List<LojaViewModel>> Execute(string pesquisa, string inicio, string intervalo)
        {
            var lojas = await _lojaRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(pesquisa))
            {
                lojas = lojas.Where(l =>
                    l.LojNome.Contains(pesquisa, StringComparison.OrdinalIgnoreCase) ||
                    (l.LojCidade != null && l.LojCidade.Contains(pesquisa, StringComparison.OrdinalIgnoreCase)) ||
                    (l.LojCNPJ != null && l.LojCNPJ.Contains(pesquisa, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return lojas.Select(l => new LojaViewModel
            {
                LojId = l.LojId,
                LojNome = l.LojNome,
                LojLogradouro = l.LojLogradouro,
                LojNumero = l.LojNumero,
                LojBairro = l.LojBairro,
                LojCidade = l.LojCidade,
                LojEstado = l.LojEstado,
                LojCEP = l.LojCEP,
                LojComplemento = l.LojComplemento,
                LojEmail = l.LojEmail,
                LojTel1 = l.LojTel1,
                LojTel2 = l.LojTel2,
                LojWhatsApp = l.LojWhatsApp,
                LojImg = l.LojImg,
                LojCNPJ = l.LojCNPJ,
                LojIE = l.LojIE,
                LojSts = l.LojSts,
                LojCorPrimaria = l.LojCorPrimaria,
                LojCorSecundaria = l.LojCorSecundaria,
                LojInstagram = l.LojInstagram,
                LojFacebook = l.LojFacebook,
                LojSlug = l.LojSlug,
                LojUrlCatalogo = l.LojUrlCatalogo,
                LojPadraoCatalogo = l.LojPadraoCatalogo
            }).ToList();
        }
    }
}
