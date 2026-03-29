using ConnectVeiculos.Core.Entities.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class NotificacaoRepository : INotificacaoRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public NotificacaoRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Notificacao>> GetByUsuarioIdAsync(int usuarioId, bool apenasNaoLidas = false)
        {
            var query = _context.Notificacoes.Where(n => n.R_UsuId == usuarioId);

            if (apenasNaoLidas)
                query = query.Where(n => !n.NotLida);

            return await query
                .OrderByDescending(n => n.NotCriadaEm)
                .Take(50)
                .ToListAsync();
        }

        public async Task<int> GetCountNaoLidasAsync(int usuarioId)
        {
            return await _context.Notificacoes
                .CountAsync(n => n.R_UsuId == usuarioId && !n.NotLida);
        }

        public async Task<Notificacao> AddAsync(Notificacao notificacao)
        {
            await _context.Notificacoes.AddAsync(notificacao);
            await _context.SaveChangesAsync();
            return notificacao;
        }

        public async Task MarcarComoLidaAsync(int notificacaoId)
        {
            var notificacao = await _context.Notificacoes.FindAsync(notificacaoId);
            if (notificacao != null)
            {
                notificacao.MarcarComoLida();
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarcarTodasComoLidasAsync(int usuarioId)
        {
            var notificacoes = await _context.Notificacoes
                .Where(n => n.R_UsuId == usuarioId && !n.NotLida)
                .ToListAsync();

            foreach (var n in notificacoes)
            {
                n.MarcarComoLida();
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAntigasLidasAsync(DateTime dataLimite)
        {
            var notificacoesAntigas = await _context.Notificacoes
                .Where(n => n.NotCriadaEm < dataLimite && n.NotLida)
                .ToListAsync();

            if (notificacoesAntigas.Any())
            {
                _context.Notificacoes.RemoveRange(notificacoesAntigas);
                await _context.SaveChangesAsync();
            }
        }
    }
}
