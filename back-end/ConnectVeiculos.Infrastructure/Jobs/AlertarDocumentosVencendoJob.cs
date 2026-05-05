using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    /// <summary>
    /// Diariamente, em cada tenant, varre VeiculoDocumento e envia notificacao
    /// SignalR + push para admins/gerentes sobre documentos com vencimento nos
    /// proximos 7 dias OU vencidos.
    /// </summary>
    public class AlertarDocumentosVencendoJob : IBackgroundJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantStore _tenantStore;
        private readonly ILogger<AlertarDocumentosVencendoJob> _logger;

        public string JobName => "AlertarDocumentosVencendo";
        public string CronExpression => "0 8 * * *";

        public AlertarDocumentosVencendoJob(
            IServiceScopeFactory scopeFactory,
            ITenantStore tenantStore,
            ILogger<AlertarDocumentosVencendoJob> logger)
        {
            _scopeFactory = scopeFactory;
            _tenantStore = tenantStore;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            var tenants = await _tenantStore.ListActiveAsync();
            foreach (var tenant in tenants)
            {
                try
                {
                    await ProcessarTenantAsync(tenant);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Tenant}] erro alertando documentos vencendo", tenant.TenSlug);
                }
            }
        }

        private async Task ProcessarTenantAsync(Core.Entities.Tenants.Tenant tenant)
        {
            using var ts = new TenantScope(_scopeFactory, tenant);
            var ctx = ts.Services.GetRequiredService<ConnectVeiculosDbContext>();
            var notificacaoService = ts.Services.GetRequiredService<INotificacaoService>();
            var pushService = ts.Services.GetRequiredService<IPushNotificationService>();

            var hoje = DateTime.Today;
            var limite = hoje.AddDays(7);

            var documentos = await ctx.VeiculosDocumentos.AsNoTracking()
                .Where(d => d.DocStatus != "CONCLUIDO"
                         && d.DocDtVencimento != null
                         && d.DocDtVencimento <= limite)
                .ToListAsync();

            if (!documentos.Any()) return;

            var vencidos = documentos.Count(d => d.DocDtVencimento < hoje);
            var proximos = documentos.Count - vencidos;

            var titulo = "Documentos com vencimento proximo";
            var corpo = vencidos > 0
                ? $"{vencidos} documento(s) vencido(s) e {proximos} vencendo nos proximos 7 dias."
                : $"{proximos} documento(s) vencendo nos proximos 7 dias.";

            await notificacaoService.EnviarParaTodosAsync("DOCUMENTOS_VENCENDO", new
            {
                titulo, corpo, vencidos, proximos, total = documentos.Count
            });

            try { await pushService.EnviarParaTodosAdminAsync(titulo, corpo, "/documentos"); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Tenant}] push falhou (provavelmente VAPID nao configurado)", tenant.TenSlug);
            }

            _logger.LogInformation("[{Tenant}] alertas: {Vencidos} vencidos, {Proximos} proximos",
                tenant.TenSlug, vencidos, proximos);
        }
    }
}
