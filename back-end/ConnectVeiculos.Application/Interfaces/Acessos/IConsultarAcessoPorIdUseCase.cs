using ConnectVeiculos.Application.ViewModels.Acessos;

namespace ConnectVeiculos.Application.Interfaces.Acessos
{
    public interface IConsultarAcessoPorIdUseCase
    {
        Task<AcessoViewModel> Execute(int id);
    }
}
