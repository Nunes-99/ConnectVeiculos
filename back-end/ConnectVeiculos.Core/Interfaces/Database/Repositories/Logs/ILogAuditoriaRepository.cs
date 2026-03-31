using ConnectVeiculos.Core.Entities.Logs;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Logs
{
    public interface ILogAuditoriaRepository
    {
        Task<int> InserirAsync(LogAuditoria log);
        Task<IEnumerable<LogAuditoria>> ConsultarAsync(string? tabela, string? acao, DateTime? dataInicio, DateTime? dataFim, int? usuarioId);
        Task<(IEnumerable<LogAuditoria> Items, int Total)> ConsultarPaginadoAsync(int page, int pageSize, string? tabela, string? acao, DateTime? dataInicio, DateTime? dataFim);
        Task<LogAuditoria?> ObterPorIdAsync(int logId);
    }
}
