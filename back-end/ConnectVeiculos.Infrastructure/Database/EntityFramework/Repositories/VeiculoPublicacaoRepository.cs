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

        // "Ativa" aqui significa "o anuncio existe na plataforma": inclui o que
        // aguarda pagamento da taxa. Filtrar so por ATIVO faria a sincronizacao
        // nao enxergar esses anuncios e criar um item DUPLICADO no ML a cada
        // clique em "Publicar veiculos disponiveis".
        private static readonly string[] StatusPublicado =
        {
            VeiculoPublicacao.StatusAtivo,
            VeiculoPublicacao.StatusAguardandoPagamento
        };

        public async Task<VeiculoPublicacao> GetAtivaByVeiculoEPlataformaAsync(int veiculoId, string plataforma)
        {
            return await _context.VeiculoPublicacoes
                .FirstOrDefaultAsync(p => p.R_VeiId == veiculoId && p.PubPlataforma == plataforma
                                          && StatusPublicado.Contains(p.PubStatus));
        }

         public async Task<VeiculoPublicacao> GetAtivaByExternoIdAsync(string externoId, string plataforma)
         {
             return await _context.VeiculoPublicacoes
                 .FirstOrDefaultAsync(p => p.PubExternoId == externoId && p.PubPlataforma == plataforma
                                           && StatusPublicado.Contains(p.PubStatus));
         }

        public async Task<IEnumerable<VeiculoPublicacao>> GetAtivasAsync()
        {
            return await _context.VeiculoPublicacoes
                .Where(p => StatusPublicado.Contains(p.PubStatus))
                .ToListAsync();
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

        public async Task<int> CountByPlataformaUltimasHorasAsync(string plataforma, int horas = 24)
        {
            var corte = DateTime.UtcNow.AddHours(-horas);
            return await _context.VeiculoPublicacoes
                .Where(p => p.PubPlataforma == plataforma
                            && p.PubDtPublicacao.HasValue
                            && p.PubDtPublicacao.Value >= corte)
                .CountAsync();
        }
    }
}
