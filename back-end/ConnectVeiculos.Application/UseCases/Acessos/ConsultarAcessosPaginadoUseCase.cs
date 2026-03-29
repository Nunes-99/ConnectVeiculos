using ConnectVeiculos.Application.Interfaces.Acessos;
using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;

namespace ConnectVeiculos.Application.UseCases.Acessos
{
    public class ConsultarAcessosPaginadoUseCase : IConsultarAcessosPaginadoUseCase
    {
        private readonly IAcessoRepository _acessoRepository;

        public ConsultarAcessosPaginadoUseCase(IAcessoRepository acessoRepository)
        {
            _acessoRepository = acessoRepository;
        }

        public async Task<PagedResult<AcessoViewModel>> Execute(int page, int pageSize, string? search = null)
        {
            var (items, total) = await _acessoRepository.GetPagedAsync(page, pageSize, search);

            var viewModels = items.Select(a => new AcessoViewModel
            {
                AcsId = a.AcsId,
                AcsNome = a.AcsNome,
                AcsDesc = a.AcsDesc ?? "",
                AcsSts = a.AcsSts
            }).ToList();

            return new PagedResult<AcessoViewModel>(viewModels, total, page, pageSize);
        }
    }
}
