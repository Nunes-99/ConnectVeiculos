using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    /// <summary>
    /// Remove tokens de recuperacao de senha expirados (mais antigos que 24h)
    /// para evitar acumulo na tabela.
    /// </summary>
    public class LimparTokensRecuperacaoJob : IBackgroundJob
    {
        private readonly ConnectVeiculosDbContext _context;
        private readonly ILogger<LimparTokensRecuperacaoJob> _logger;

        public string JobName => "LimparTokensRecuperacao";
        public string CronExpression => "0 4 * * *"; // Diariamente as 4h da manha

        public LimparTokensRecuperacaoJob(
            ConnectVeiculosDbContext context,
            ILogger<LimparTokensRecuperacaoJob> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                var limite = DateTime.Now.AddHours(-24);
                var antigos = await _context.RecuperacoesSenha
                    .Where(r => r.RecDataCriacao < limite || r.RecUtilizado)
                    .ToListAsync();

                if (antigos.Count == 0)
                {
                    _logger.LogInformation("Nenhum token de recuperacao para limpar.");
                    return;
                }

                _context.RecuperacoesSenha.RemoveRange(antigos);
                await _context.SaveChangesAsync();
                _logger.LogInformation("{Count} tokens de recuperacao removidos.", antigos.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar tokens de recuperacao.");
                throw;
            }
        }
    }
}
