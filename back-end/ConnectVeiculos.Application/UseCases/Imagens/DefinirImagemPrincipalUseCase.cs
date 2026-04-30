using ConnectVeiculos.Application.Interfaces.Imagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;

namespace ConnectVeiculos.Application.UseCases.Imagens
{
    public class DefinirImagemPrincipalUseCase : IDefinirImagemPrincipalUseCase
    {
        private readonly IVeiculoImagemRepository _imagemRepository;

        public DefinirImagemPrincipalUseCase(IVeiculoImagemRepository imagemRepository)
        {
            _imagemRepository = imagemRepository;
        }

        public async Task Execute(int imagemId)
        {
            var imagem = await _imagemRepository.GetByIdAsync(imagemId);
            if (imagem == null) return;

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(imagem.R_VeiId);
            var listaOrdenada = imagens.OrderBy(i => i.ImgOrdem).ToList();

            // Reordenar: a imagem selecionada vai para ordem 1, as demais seguem em sequência
            var ordem = 1;
            // Primeiro a imagem principal
            imagem.SetProperties(imagem.ImgId, imagem.R_VeiId, imagem.ImgCaminho, ordem, imagem.ImgSts);
            await _imagemRepository.UpdateAsync(imagem);
            ordem++;

            // Depois as demais na ordem original
            foreach (var img in listaOrdenada.Where(i => i.ImgId != imagemId))
            {
                img.SetProperties(img.ImgId, img.R_VeiId, img.ImgCaminho, ordem, img.ImgSts);
                await _imagemRepository.UpdateAsync(img);
                ordem++;
            }
        }
    }
}
