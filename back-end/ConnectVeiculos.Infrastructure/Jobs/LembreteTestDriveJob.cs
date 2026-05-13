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
    /// Roda diariamente as 9h. Em cada tenant, pega test drives Confirmados que
    /// acontecem AMANHA e dispara o template WhatsApp "testdrive_lembrete".
    /// Cliente recebe lembrete 24h antes — reduz no-show.
    /// Idempotente: usa coluna interna pra marcar quais ja foram lembrados.
    /// </summary>
    public class LembreteTestDriveJob : IBackgroundJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantStore _tenantStore;
        private readonly ILogger<LembreteTestDriveJob> _logger;

        public string JobName => "LembreteTestDrive";
        public string CronExpression => "0 9 * * *"; // todo dia as 9h

        public LembreteTestDriveJob(
            IServiceScopeFactory scopeFactory,
            ITenantStore tenantStore,
            ILogger<LembreteTestDriveJob> logger)
        {
            _scopeFactory = scopeFactory;
            _tenantStore = tenantStore;
            _logger = logger;
        }

        public Task ExecuteAsync()
        {
            return MultiTenantJobExecutor.RunAsync(JobName, _tenantStore, _scopeFactory, _logger,
                async (ts, tenant) =>
                {
                    var ctx = ts.Services.GetRequiredService<ConnectVeiculosDbContext>();
                    var notifService = ts.Services.GetRequiredService<ITestDriveNotificacaoService>();

                    var amanha = DateTime.Today.AddDays(1);
                    var depoisAmanha = amanha.AddDays(1);

                    var testDrives = await ctx.TestDrives
                        .Where(t => t.TdrStatus == "C"
                                 && t.TdrDataAgendamento >= amanha
                                 && t.TdrDataAgendamento < depoisAmanha)
                        .ToListAsync();

                    if (testDrives.Count == 0) return;

                    int enviados = 0, falhas = 0, semConfig = 0;
                    foreach (var td in testDrives)
                    {
                        var r = await notifService.NotificarLembreteAsync(td);
                        if (r.Enviada) enviados++;
                        else if (r.Motivo == "nao-configurado") semConfig++;
                        else falhas++;
                    }

                    _logger.LogInformation(
                        "Lembrete TestDrive tenant {Slug}: {Total} agendados pra amanha — {Enviados} enviados, {Falhas} falharam, {SemConfig} sem WA configurado",
                        tenant.TenSlug, testDrives.Count, enviados, falhas, semConfig);
                });
        }
    }
}
