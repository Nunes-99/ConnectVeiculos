using ConnectVeiculos.Application.ViewModels.Imagens;

namespace ConnectVeiculos.Application.Interfaces.Imagens
{
    public interface IConsultarImagensVeiculoUseCase
    {
        Task<IEnumerable<VeiculoImagemViewModel>> Execute(int veiculoId);
    }
}
