using ConnectVeiculos.Application.Interfaces.Imagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;

namespace ConnectVeiculos.Application.UseCases.Imagens
{
    public class ExcluirImagemVeiculoUseCase : IExcluirImagemVeiculoUseCase
    {
        private readonly IVeiculoImagemRepository _imagemRepository;

        public ExcluirImagemVeiculoUseCase(IVeiculoImagemRepository imagemRepository)
        {
            _imagemRepository = imagemRepository;
        }

        public async Task Execute(int imagemId)
        {
            await _imagemRepository.DeleteAsync(imagemId);
        }
    }
}
