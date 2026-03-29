using ConnectVeiculos.Core.Entities.Webhooks;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class WebhookRepository : IWebhookRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public WebhookRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Webhook>> GetAllAsync()
        {
            return await _context.Webhooks
                .OrderByDescending(w => w.WebCriadoEm)
                .ToListAsync();
        }

        public async Task<IEnumerable<Webhook>> GetActiveByEventoAsync(string evento)
        {
            return await _context.Webhooks
                .Where(w => w.WebAtivo && w.WebEventos.Contains(evento))
                .ToListAsync();
        }

        public async Task<Webhook> GetByIdAsync(int id)
        {
            return await _context.Webhooks.FindAsync(id);
        }

        public async Task<Webhook> AddAsync(Webhook webhook)
        {
            await _context.Webhooks.AddAsync(webhook);
            await _context.SaveChangesAsync();
            return webhook;
        }

        public async Task UpdateAsync(Webhook webhook)
        {
            _context.Webhooks.Update(webhook);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var webhook = await _context.Webhooks.FindAsync(id);
            if (webhook != null)
            {
                _context.Webhooks.Remove(webhook);
                await _context.SaveChangesAsync();
            }
        }
    }
}
