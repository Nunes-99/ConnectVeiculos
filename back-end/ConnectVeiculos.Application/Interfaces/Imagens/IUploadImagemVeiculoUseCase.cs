using ConnectVeiculos.Application.ViewModels.Imagens;

namespace ConnectVeiculos.Application.Interfaces.Imagens
{
    public interface IUploadImagemVeiculoUseCase
    {
        Task<VeiculoImagemViewModel> Execute(int veiculoId, string caminhoArquivo);
    }
}
