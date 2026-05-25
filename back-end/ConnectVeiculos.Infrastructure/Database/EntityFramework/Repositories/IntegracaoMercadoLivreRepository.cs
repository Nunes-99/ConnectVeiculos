using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class IntegracaoMercadoLivreRepository : IIntegracaoMercadoLivreRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public IntegracaoMercadoLivreRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public Task<IntegracaoMercadoLivre> GetSingletonAsync()
            => _context.IntegracoesMercadoLivre.FirstOrDefaultAsync();

        public async Task<IntegracaoMercadoLivre> EnsureSingletonAsync()
        {
            var existente = await GetSingletonAsync();
            if (existente != null) return existente;

            var novo = new IntegracaoMercadoLivre
            {
                IntStatus = StatusIntegracao.Inativa,
                IntMotivoErro = MotivoIntegracaoErro.Nenhum,
                IntCriadaEm = DateTime.UtcNow
            };
            _context.IntegracoesMercadoLivre.Add(novo);
            await _context.SaveChangesAsync();
            return novo;
        }

        public async Task UpdateAsync(IntegracaoMercadoLivre integracao)
        {
            integracao.IntAtualizadaEm = DateTime.UtcNow;
            _context.IntegracoesMercadoLivre.Update(integracao);
            await _context.SaveChangesAsync();
        }
    }
}
