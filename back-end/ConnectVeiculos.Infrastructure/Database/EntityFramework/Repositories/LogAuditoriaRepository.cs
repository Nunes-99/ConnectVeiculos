using ConnectVeiculos.Core.Entities.Logs;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Logs;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class LogAuditoriaRepository : ILogAuditoriaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public LogAuditoriaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<int> InserirAsync(LogAuditoria log)
        {
            _context.LogsAuditoria.Add(log);
            await _context.SaveChangesAsync();
            return log.LogId;
        }

        public async Task<IEnumerable<LogAuditoria>> ConsultarAsync(string? tabela, string? acao, DateTime? dataInicio, DateTime? dataFim, int? usuarioId)
        {
            var query = _context.LogsAuditoria.AsQueryable();

            if (!string.IsNullOrWhiteSpace(tabela))
                query = query.Where(l => l.LogTabela == tabela);

            if (!string.IsNullOrWhiteSpace(acao))
                query = query.Where(l => l.LogAcao == acao);

            if (dataInicio.HasValue)
                query = query.Where(l => l.LogDataHora >= dataInicio.Value);

            if (dataFim.HasValue)
                query = query.Where(l => l.LogDataHora <= dataFim.Value);

            if (usuarioId.HasValue)
                query = query.Where(l => l.LogUsuarioId == usuarioId.Value);

            return await query.OrderByDescending(l => l.LogDataHora).Take(1000).ToListAsync();
        }

        public async Task<(IEnumerable<LogAuditoria> Items, int Total)> ConsultarPaginadoAsync(int page, int pageSize, string? tabela, string? acao, DateTime? dataInicio, DateTime? dataFim)
        {
            var query = _context.LogsAuditoria.AsQueryable();

            if (!string.IsNullOrWhiteSpace(tabela))
                query = query.Where(l => l.LogTabela == tabela);

            if (!string.IsNullOrWhiteSpace(acao))
                query = query.Where(l => l.LogAcao == acao);

            if (dataInicio.HasValue)
                query = query.Where(l => l.LogDataHora >= dataInicio.Value);

            if (dataFim.HasValue)
                query = query.Where(l => l.LogDataHora <= dataFim.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.LogDataHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<LogAuditoria?> ObterPorIdAsync(int logId)
        {
            return await _context.LogsAuditoria.FirstOrDefaultAsync(l => l.LogId == logId);
        }
    }
}
