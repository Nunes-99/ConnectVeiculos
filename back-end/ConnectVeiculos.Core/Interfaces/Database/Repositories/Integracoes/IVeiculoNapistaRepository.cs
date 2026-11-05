using ConnectVeiculos.Core.Entities.Integracoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes
{
    public interface IVeiculoNapistaRepository
    {
        Task<VeiculoNapista?> GetByVeiculoIdAsync(int veiculoId);
        Task SalvarAsync(VeiculoNapista anuncio);
    }
}
