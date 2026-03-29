using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Dashboard;
using ConnectVeiculos.Infrastructure.Jobs;
using Microsoft.AspNetCore.Http;

namespace ConnectVeiculos.API.Extensions
{
    public static class HangfireExtensions
    {
        public static IServiceCollection AddCustomHangfire(this IServiceCollection services)
        {
            // Configurar Hangfire com armazenamento em memoria (para dev)
            // Em producao, usar Redis ou SQL Server
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMemoryStorage());

            services.AddHangfireServer(options =>
            {
                options.WorkerCount = 2;
                options.Queues = new[] { "default", "critical" };
            });

            // Registrar jobs
            services.AddScoped<LimparRefreshTokensJob>();
            services.AddScoped<LimparNotificacoesAntigasJob>();
            services.AddScoped<AtualizarCacheFipeJob>();

            return services;
        }

        public static IApplicationBuilder UseCustomHangfire(this IApplicationBuilder app)
        {
            // Dashboard do Hangfire (apenas para admins em producao)
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new HangfireAuthorizationFilter() },
                DashboardTitle = "ConnectVeiculos - Background Jobs"
            });

            // Agendar jobs recorrentes
            ConfigureRecurringJobs();

            return app;
        }

        private static void ConfigureRecurringJobs()
        {
            // Limpar refresh tokens expirados - Diariamente as 3h
            RecurringJob.AddOrUpdate<LimparRefreshTokensJob>(
                "limpar-refresh-tokens",
                job => job.ExecuteAsync(),
                Cron.Daily(3));

            // Limpar notificacoes antigas - Domingos as 4h
            RecurringJob.AddOrUpdate<LimparNotificacoesAntigasJob>(
                "limpar-notificacoes-antigas",
                job => job.ExecuteAsync(),
                Cron.Weekly(DayOfWeek.Sunday, 4));

            // Atualizar cache FIPE - Primeiro dia do mes as 2h
            RecurringJob.AddOrUpdate<AtualizarCacheFipeJob>(
                "atualizar-cache-fipe",
                job => job.ExecuteAsync(),
                Cron.Monthly(1, 2));
        }
    }

    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            if (httpContext == null)
                return false;

            // Em desenvolvimento, permitir acesso
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                return true;

            // Em producao, verificar se usuario esta autenticado e e admin
            return httpContext.User.Identity?.IsAuthenticated == true
                && httpContext.User.IsInRole("Administrador");
        }
    }
}
