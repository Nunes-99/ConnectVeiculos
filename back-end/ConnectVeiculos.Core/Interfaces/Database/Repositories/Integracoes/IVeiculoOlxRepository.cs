using ConnectVeiculos.Core.Entities.Integracoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes
{
    public interface IVeiculoOlxRepository
    {
        Task<VeiculoOlx?> GetByVeiculoIdAsync(int veiculoId);
        Task SalvarAsync(VeiculoOlx anuncio);

        /// <summary>Veiculos com anuncio na OLX (selo "OLX" na lista).</summary>
        Task<HashSet<int>> VeiculosNoArAsync();
    }
}
