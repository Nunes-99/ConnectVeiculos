using ConnectVeiculos.Core.Entities.Integracoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes
{
    public interface IVeiculoNapistaRepository
    {
        Task<VeiculoNapista?> GetByVeiculoIdAsync(int veiculoId);
        Task SalvarAsync(VeiculoNapista anuncio);

        /// <summary>anuncioId do NaPista → veiculo ja ligado a ele (qualquer situacao).</summary>
        Task<Dictionary<string, int>> VeiculosPorAnuncioAsync(IReadOnlyCollection<string> anuncioIds);
    }
}
