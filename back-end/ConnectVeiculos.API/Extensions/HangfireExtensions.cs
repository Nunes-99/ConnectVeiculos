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
            services.AddScoped<AlertarDocumentosVencendoJob>();
            services.AddScoped<LimparTokensRecuperacaoJob>();

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

            // Alertar documentos vencendo - Diariamente as 8h
            RecurringJob.AddOrUpdate<AlertarDocumentosVencendoJob>(
                "alertar-documentos-vencendo",
                job => job.ExecuteAsync(),
                Cron.Daily(8));

            // Limpar tokens de recuperacao expirados - Diariamente as 4h
            RecurringJob.AddOrUpdate<LimparTokensRecuperacaoJob>(
                "limpar-tokens-recuperacao",
                job => job.ExecuteAsync(),
                Cron.Daily(4));
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

            // Em producao: Basic Auth via env vars (HANGFIRE_USER / HANGFIRE_PASSWORD).
            // Se nao configurado, dashboard fica fechado para todos.
            var expectedUser = Environment.GetEnvironmentVariable("HANGFIRE_USER");
            var expectedPwd = Environment.GetEnvironmentVariable("HANGFIRE_PASSWORD");
            if (string.IsNullOrEmpty(expectedUser) || string.IsNullOrEmpty(expectedPwd))
                return false;

            var header = httpContext.Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire\"";
                httpContext.Response.StatusCode = 401;
                return false;
            }

            try
            {
                var encoded = header.Substring("Basic ".Length).Trim();
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                var parts = decoded.Split(':', 2);
                if (parts.Length != 2) return false;

                return string.Equals(parts[0], expectedUser, StringComparison.Ordinal)
                    && string.Equals(parts[1], expectedPwd, StringComparison.Ordinal);
            }
            catch { return false; }
        }
    }
}
