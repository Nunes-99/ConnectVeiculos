using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Veiculos;

namespace ConnectVeiculos.Application.Interfaces.Veiculos
{
    public interface IConsultarVeiculosPaginadoUseCase
    {
        Task<PagedResult<VeiculoViewModel>> Execute(int page, int pageSize, string? search = null, int? lojaId = null);
    }
}
