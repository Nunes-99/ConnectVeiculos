using ConnectVeiculos.Application.Interfaces.Imagens;
using ConnectVeiculos.Application.ViewModels.Imagens;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;

namespace ConnectVeiculos.Application.UseCases.Imagens
{
    public class UploadImagemVeiculoUseCase : IUploadImagemVeiculoUseCase
    {
        private readonly IVeiculoImagemRepository _imagemRepository;

        public UploadImagemVeiculoUseCase(IVeiculoImagemRepository imagemRepository)
        {
            _imagemRepository = imagemRepository;
        }

        public async Task<VeiculoImagemViewModel> Execute(int veiculoId, string caminhoArquivo)
        {
            // Obter proxima ordem
            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var ordem = imagens.Any() ? imagens.Max(i => i.ImgOrdem) + 1 : 1;

            var imagem = new VeiculoImagem(0, veiculoId, caminhoArquivo, ordem, true);
            var id = await _imagemRepository.CreateAsync(imagem);

            return new VeiculoImagemViewModel
            {
                ImgId = id,
                R_VeiId = veiculoId,
                ImgCaminho = caminhoArquivo,
                ImgOrdem = ordem
            };
        }
    }
}
