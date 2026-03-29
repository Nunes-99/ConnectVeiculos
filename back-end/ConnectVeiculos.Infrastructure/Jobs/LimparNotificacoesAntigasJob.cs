using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    public class LimparNotificacoesAntigasJob : IBackgroundJob
    {
        private readonly INotificacaoRepository _notificacaoRepository;
        private readonly ILogger<LimparNotificacoesAntigasJob> _logger;
        private const int DiasRetencao = 30;

        public string JobName => "LimparNotificacoesAntigas";
        public string CronExpression => "0 4 * * 0"; // Domingos as 4h da manha

        public LimparNotificacoesAntigasJob(
            INotificacaoRepository notificacaoRepository,
            ILogger<LimparNotificacoesAntigasJob> logger)
        {
            _notificacaoRepository = notificacaoRepository;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Iniciando limpeza de notificacoes lidas com mais de {Dias} dias...", DiasRetencao);

            try
            {
                var dataLimite = DateTime.UtcNow.AddDays(-DiasRetencao);
                await _notificacaoRepository.DeleteAntigasLidasAsync(dataLimite);
                _logger.LogInformation("Limpeza de notificacoes antigas concluida.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar notificacoes antigas.");
                throw;
            }
        }
    }
}
