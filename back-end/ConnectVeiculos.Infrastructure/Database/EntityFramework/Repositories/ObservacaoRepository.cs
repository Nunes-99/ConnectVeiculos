using ConnectVeiculos.Core.Entities.Observacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Observacoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class ObservacaoRepository : IObservacaoRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public ObservacaoRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<Observacao> GetByIdAsync(int id)
        {
            return await _context.Observacoes.FirstOrDefaultAsync(o => o.ObsId == id);
        }

        public async Task<IEnumerable<Observacao>> GetAllAsync()
        {
            return await _context.Observacoes.ToListAsync();
        }

        public async Task<int> CreateAsync(Observacao observacao)
        {
            _context.Observacoes.Add(observacao);
            await _context.SaveChangesAsync();
            return observacao.ObsId;
        }

        public async Task UpdateAsync(Observacao observacao)
        {
            _context.Observacoes.Update(observacao);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var observacao = await GetByIdAsync(id);
            if (observacao != null)
            {
                observacao.AlterarStatus(false);
                await UpdateAsync(observacao);
            }
        }
    }
}
