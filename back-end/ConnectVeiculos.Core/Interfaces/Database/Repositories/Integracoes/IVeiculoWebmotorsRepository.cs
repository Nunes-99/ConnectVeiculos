using ConnectVeiculos.Core.Entities.Integracoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes
{
    public interface IVeiculoWebmotorsRepository
    {
        Task<VeiculoWebmotors?> GetByVeiculoIdAsync(int veiculoId);
        Task<IReadOnlyList<VeiculoWebmotors>> ListarPublicadosAsync();
        Task SalvarAsync(VeiculoWebmotors anuncio);
    }
}
