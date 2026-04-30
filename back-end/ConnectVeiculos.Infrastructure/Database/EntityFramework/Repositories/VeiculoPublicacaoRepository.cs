using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VeiculoPublicacaoRepository : IVeiculoPublicacaoRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoPublicacaoRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<VeiculoPublicacao> GetByIdAsync(int id)
        {
            return await _context.VeiculoPublicacoes.FirstOrDefaultAsync(p => p.PubId == id);
        }

        public async Task<IEnumerable<VeiculoPublicacao>> GetByVeiculoIdAsync(int veiculoId)
        {
            return await _context.VeiculoPublicacoes
                .Where(p => p.R_VeiId == veiculoId)
                .OrderByDescending(p => p.PubDtPublicacao)
                .ToListAsync();
        }

        public async Task<VeiculoPublicacao> GetAtivaByVeiculoEPlataformaAsync(int veiculoId, string plataforma)
        {
            return await _context.VeiculoPublicacoes
                .FirstOrDefaultAsync(p => p.R_VeiId == veiculoId && p.PubPlataforma == plataforma && p.PubStatus == "ATIVO");
        }

        public async Task<int> CreateAsync(VeiculoPublicacao publicacao)
        {
            _context.VeiculoPublicacoes.Add(publicacao);
            await _context.SaveChangesAsync();
            return publicacao.PubId;
        }

        public async Task UpdateAsync(VeiculoPublicacao publicacao)
        {
            _context.VeiculoPublicacoes.Update(publicacao);
            await _context.SaveChangesAsync();
        }
    }
}
