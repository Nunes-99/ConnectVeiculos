using ConnectVeiculos.Core.Interfaces.Database.Repositories.RefreshTokens;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    public class LimparRefreshTokensJob : IBackgroundJob
    {
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly ILogger<LimparRefreshTokensJob> _logger;

        public string JobName => "LimparRefreshTokensExpirados";
        public string CronExpression => "0 3 * * *"; // Diariamente as 3h da manha

        public LimparRefreshTokensJob(
            IRefreshTokenRepository refreshTokenRepository,
            ILogger<LimparRefreshTokensJob> logger)
        {
            _refreshTokenRepository = refreshTokenRepository;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Iniciando limpeza de refresh tokens expirados...");

            try
            {
                await _refreshTokenRepository.DeleteExpiredTokensAsync();
                _logger.LogInformation("Limpeza de refresh tokens expirados concluida.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar refresh tokens expirados.");
                throw;
            }
        }
    }
}
