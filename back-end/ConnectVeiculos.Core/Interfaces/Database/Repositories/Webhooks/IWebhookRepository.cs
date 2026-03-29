using ConnectVeiculos.Core.Entities.Webhooks;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Webhooks
{
    public interface IWebhookRepository
    {
        Task<IEnumerable<Webhook>> GetAllAsync();
        Task<IEnumerable<Webhook>> GetActiveByEventoAsync(string evento);
        Task<Webhook> GetByIdAsync(int id);
        Task<Webhook> AddAsync(Webhook webhook);
        Task UpdateAsync(Webhook webhook);
        Task DeleteAsync(int id);
    }
}
