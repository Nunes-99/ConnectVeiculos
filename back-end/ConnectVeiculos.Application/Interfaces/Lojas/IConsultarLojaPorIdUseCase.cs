using ConnectVeiculos.Application.ViewModels.Lojas;

namespace ConnectVeiculos.Application.Interfaces.Lojas
{
    public interface IConsultarLojaPorIdUseCase
    {
        Task<LojaViewModel> Execute(int id);
    }
}
