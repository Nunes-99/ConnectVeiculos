using ConnectVeiculos.Application.ViewModels.Acessos;

namespace ConnectVeiculos.Application.Interfaces.Acessos
{
    public interface IConsultarAcessosUseCase
    {
        Task<List<AcessoViewModel>> Execute(string pesquisa, string inicio, string intervalo);
    }
}
