using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    /// <summary>
    /// Diariamente, varre VeiculoDocumento e envia notificacao SignalR + push para
    /// admins/gerentes sobre documentos com vencimento nos proximos 7 dias OU vencidos.
    /// </summary>
    public class AlertarDocumentosVencendoJob : IBackgroundJob
    {
        private readonly ConnectVeiculosDbContext _context;
        private readonly INotificacaoService _notificacaoService;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<AlertarDocumentosVencendoJob> _logger;

        public string JobName => "AlertarDocumentosVencendo";
        public string CronExpression => "0 8 * * *"; // Diariamente as 8h da manha

        public AlertarDocumentosVencendoJob(
            ConnectVeiculosDbContext context,
            INotificacaoService notificacaoService,
            IPushNotificationService pushService,
            ILogger<AlertarDocumentosVencendoJob> logger)
        {
            _context = context;
            _notificacaoService = notificacaoService;
            _pushService = pushService;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Iniciando varredura de documentos vencendo...");
            try
            {
                var hoje = DateTime.Today;
                var limite = hoje.AddDays(7);

                var documentos = await _context.VeiculosDocumentos.AsNoTracking()
                    .Where(d => d.DocStatus != "CONCLUIDO"
                             && d.DocDtVencimento != null
                             && d.DocDtVencimento <= limite)
                    .ToListAsync();

                if (!documentos.Any())
                {
                    _logger.LogInformation("Nenhum documento vencendo nos proximos 7 dias.");
                    return;
                }

                var vencidos = documentos.Count(d => d.DocDtVencimento < hoje);
                var proximos = documentos.Count - vencidos;

                var titulo = "Documentos com vencimento proximo";
                var corpo = vencidos > 0
                    ? $"{vencidos} documento(s) vencido(s) e {proximos} vencendo nos proximos 7 dias."
                    : $"{proximos} documento(s) vencendo nos proximos 7 dias.";

                await _notificacaoService.EnviarParaTodosAsync("DOCUMENTOS_VENCENDO", new
                {
                    titulo,
                    corpo,
                    vencidos,
                    proximos,
                    total = documentos.Count
                });

                try { await _pushService.EnviarParaTodosAdminAsync(titulo, corpo, "/documentos"); }
                catch (Exception ex) { _logger.LogWarning(ex, "Push falhou (provavelmente VAPID nao configurado)"); }

                _logger.LogInformation("Alertas enviados: {Vencidos} vencidos, {Proximos} proximos.", vencidos, proximos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alertar documentos vencendo.");
                throw;
            }
        }
    }
}
