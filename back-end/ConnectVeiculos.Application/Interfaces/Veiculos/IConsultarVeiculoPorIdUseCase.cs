using ConnectVeiculos.Application.ViewModels.Veiculos;

namespace ConnectVeiculos.Application.Interfaces.Veiculos
{
    public interface IConsultarVeiculoPorIdUseCase
    {
        Task<VeiculoViewModel> Execute(int id);
    }
}
