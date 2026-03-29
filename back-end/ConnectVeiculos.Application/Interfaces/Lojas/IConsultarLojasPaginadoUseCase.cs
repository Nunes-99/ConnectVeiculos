using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Lojas;

namespace ConnectVeiculos.Application.Interfaces.Lojas
{
    public interface IConsultarLojasPaginadoUseCase
    {
        Task<PagedResult<LojaViewModel>> Execute(int page, int pageSize, string? search = null);
    }
}
