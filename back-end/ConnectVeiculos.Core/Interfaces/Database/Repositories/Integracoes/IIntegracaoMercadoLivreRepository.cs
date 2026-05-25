using ConnectVeiculos.Core.Entities.Integracoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes
{
    public interface IIntegracaoMercadoLivreRepository
    {
        // Retorna o singleton do tenant (1 integracao ML por base). Null se ainda
        // nao foi criada — chamadores chamam EnsureSingletonAsync para criar.
        Task<IntegracaoMercadoLivre> GetSingletonAsync();
        Task<IntegracaoMercadoLivre> EnsureSingletonAsync();
        Task UpdateAsync(IntegracaoMercadoLivre integracao);
    }
}
