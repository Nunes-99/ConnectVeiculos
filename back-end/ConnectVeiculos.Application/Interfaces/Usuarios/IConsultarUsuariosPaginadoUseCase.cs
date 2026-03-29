using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Usuarios;

namespace ConnectVeiculos.Application.Interfaces.Usuarios
{
    public interface IConsultarUsuariosPaginadoUseCase
    {
        Task<PagedResult<UsuarioViewModel>> Execute(int page, int pageSize, string? search = null);
    }
}
