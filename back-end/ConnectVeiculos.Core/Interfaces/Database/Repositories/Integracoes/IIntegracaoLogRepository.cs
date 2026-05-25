using ConnectVeiculos.Core.Entities.Integracoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes
{
    public interface IIntegracaoLogRepository
    {
        Task CreateAsync(IntegracaoLog log);

        // Logs mais recentes primeiro. Limit pra UI nao baixar historico inteiro.
        Task<IReadOnlyList<IntegracaoLog>> GetUltimosAsync(int limit = 100);
    }
}
