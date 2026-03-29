using ConnectVeiculos.Application.Interfaces.Imagens;
using ConnectVeiculos.Application.ViewModels.Imagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;

namespace ConnectVeiculos.Application.UseCases.Imagens
{
    public class ConsultarImagensVeiculoUseCase : IConsultarImagensVeiculoUseCase
    {
        private readonly IVeiculoImagemRepository _imagemRepository;

        public ConsultarImagensVeiculoUseCase(IVeiculoImagemRepository imagemRepository)
        {
            _imagemRepository = imagemRepository;
        }

        public async Task<IEnumerable<VeiculoImagemViewModel>> Execute(int veiculoId)
        {
            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);

            return imagens
                .Where(i => i.ImgSts)
                .Select(i => new VeiculoImagemViewModel
                {
                    ImgId = i.ImgId,
                    R_VeiId = i.R_VeiId,
                    ImgCaminho = i.ImgCaminho,
                    ImgOrdem = i.ImgOrdem
                })
                .OrderBy(i => i.ImgOrdem);
        }
    }
}
