using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;

namespace ConnectVeiculos.Application.UseCases.Lojas
{
    public class ConsultarLojasPaginadoUseCase : IConsultarLojasPaginadoUseCase
    {
        private readonly ILojaRepository _lojaRepository;

        public ConsultarLojasPaginadoUseCase(ILojaRepository lojaRepository)
        {
            _lojaRepository = lojaRepository;
        }

        public async Task<PagedResult<LojaViewModel>> Execute(int page, int pageSize, string? search = null)
        {
            var (items, total) = await _lojaRepository.GetPagedAsync(page, pageSize, search);

            var viewModels = items.Select(l => new LojaViewModel
            {
                LojId = l.LojId,
                LojNome = l.LojNome,
                LojLogradouro = l.LojLogradouro ?? "",
                LojNumero = l.LojNumero ?? "",
                LojBairro = l.LojBairro ?? "",
                LojCidade = l.LojCidade ?? "",
                LojEstado = l.LojEstado ?? "",
                LojCEP = l.LojCEP ?? "",
                LojComplemento = l.LojComplemento ?? "",
                LojEmail = l.LojEmail ?? "",
                LojTel1 = l.LojTel1 ?? "",
                LojTel2 = l.LojTel2 ?? "",
                LojWhatsApp = l.LojWhatsApp ?? "",
                LojImg = l.LojImg ?? "",
                LojCNPJ = l.LojCNPJ ?? "",
                LojIE = l.LojIE ?? "",
                LojSts = l.LojSts,
                LojCorPrimaria = l.LojCorPrimaria,
                LojCorSecundaria = l.LojCorSecundaria,
                LojInstagram = l.LojInstagram,
                LojFacebook = l.LojFacebook,
                LojSlug = l.LojSlug
            }).ToList();

            return new PagedResult<LojaViewModel>(viewModels, total, page, pageSize);
        }
    }
}
