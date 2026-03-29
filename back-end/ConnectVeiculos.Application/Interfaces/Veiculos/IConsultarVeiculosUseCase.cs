using ConnectVeiculos.Application.ViewModels.Veiculos;

namespace ConnectVeiculos.Application.Interfaces.Veiculos
{
    public interface IConsultarVeiculosUseCase
    {
        Task<List<VeiculoViewModel>> Execute(string pesquisa, int? lojaId, string inicio, string intervalo);
    }
}
