using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Veiculos;

namespace ConnectVeiculos.Application.Interfaces.Veiculos
{
    public interface IBuscaAvancadaVeiculosUseCase
    {
        Task<PagedResult<VeiculoViewModel>> Execute(BuscaAvancadaVeiculoInputModel input);
    }
}
