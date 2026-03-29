using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConnectVeiculos.API.Extensions
{
    public static class HealthChecksExtensions
    {
        public static IServiceCollection AddCustomHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var healthChecksBuilder = services.AddHealthChecks();

            // Health check do banco de dados (SQLite por padrao)
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrEmpty(connectionString))
            {
                // Se for PostgreSQL (producao)
                if (connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    healthChecksBuilder.AddNpgSql(
                        connectionString,
                        name: "database",
                        failureStatus: HealthStatus.Unhealthy,
                        tags: new[] { "db", "sql", "postgres" });
                }
            }

            // Health check do Redis (se configurado)
            var redisConnection = configuration.GetSection("RedisSettings:ConnectionString").Value;
            if (!string.IsNullOrEmpty(redisConnection) && redisConnection != "localhost:6379")
            {
                healthChecksBuilder.AddRedis(
                    redisConnection,
                    name: "redis",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "cache", "redis" });
            }

            // Health check da API FIPE
            healthChecksBuilder.AddUrlGroup(
                new Uri("https://parallelum.com.br/fipe/api/v1/carros/marcas"),
                name: "fipe-api",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "external", "fipe" });

            // Health check customizado para memoria
            healthChecksBuilder.AddCheck("memory", () =>
            {
                var allocated = GC.GetTotalMemory(false);
                var threshold = 1024L * 1024L * 1024L; // 1 GB

                if (allocated < threshold)
                {
                    return HealthCheckResult.Healthy($"Memoria alocada: {allocated / 1024 / 1024} MB");
                }

                return HealthCheckResult.Degraded($"Memoria alocada alta: {allocated / 1024 / 1024} MB");
            }, tags: new[] { "memory" });

            return services;
        }

        public static IEndpointRouteBuilder MapCustomHealthChecks(this IEndpointRouteBuilder endpoints)
        {
            // Endpoint de health check simples (para load balancers)
            endpoints.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => false, // Nao executa nenhum check, apenas retorna 200
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"status\":\"healthy\"}");
                }
            });

            // Endpoint de health check detalhado
            endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = WriteHealthCheckResponse
            });

            // Health check apenas do banco
            endpoints.MapHealthChecks("/health/db", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("db"),
                ResponseWriter = WriteHealthCheckResponse
            });

            // Health check de servicos externos
            endpoints.MapHealthChecks("/health/external", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("external"),
                ResponseWriter = WriteHealthCheckResponse
            });

            return endpoints;
        }

        private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var result = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description,
                    exception = e.Value.Exception?.Message,
                    tags = e.Value.Tags
                })
            };

            await context.Response.WriteAsJsonAsync(result);
        }
    }
}
