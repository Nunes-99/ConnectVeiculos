using ConnectVeiculos.Application.ViewModels.Lojas;

namespace ConnectVeiculos.Application.Interfaces.Lojas
{
    public interface IConsultarLojasUseCase
    {
        Task<List<LojaViewModel>> Execute(string pesquisa, string inicio, string intervalo);
    }
}
