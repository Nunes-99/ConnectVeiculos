using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Acessos;

namespace ConnectVeiculos.Application.Interfaces.Acessos
{
    public interface IConsultarAcessosPaginadoUseCase
    {
        Task<PagedResult<AcessoViewModel>> Execute(int page, int pageSize, string? search = null);
    }
}
