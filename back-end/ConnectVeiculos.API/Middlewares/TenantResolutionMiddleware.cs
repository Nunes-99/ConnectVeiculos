using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;

namespace ConnectVeiculos.API.Middlewares
{
    /// <summary>
    /// Le o Host header do request, extrai o subdomain e popula o ITenantContext
    /// com o tenant resolvido. Subdomain "" / "www" / IP nu mapeiam para o tenant
    /// "default" — preserva compatibilidade com a URL principal connectveiculos.dev.br.
    ///
    /// Tenants suspensos retornam 503. Subdomains desconhecidos retornam 404.
    ///
    /// Rotas que NAO precisam de tenant (health check, swagger, ACME challenge)
    /// devem ser configuradas para rodar ANTES desse middleware no pipeline.
    /// </summary>
    public sealed class TenantResolutionMiddleware
    {
        private const string DefaultTenantSlug = "default";

        private readonly RequestDelegate _next;
        private readonly ILogger<TenantResolutionMiddleware> _logger;

        public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantStore store, ITenantContext tenantContext)
        {
            // Rotas que NAO precisam de tenant: ACME challenge, swagger, health.
            // Estas tipicamente rodam ANTES desse middleware no pipeline, mas
            // como protecao extra, deixamos passar sem resolver tenant.
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/.well-known/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var slug = ExtractTenantSlug(context.Request.Host.Host);

            var tenant = await store.GetBySlugAsync(slug, context.RequestAborted);
            if (tenant == null)
            {
                // Master vazio + slug "default" = primeiro boot apos a refatoracao,
                // antes da migracao final do tenant default. Deixa cair no fallback
                // do TenantConnectionFactory (DefaultConnection do appsettings).
                if (slug == DefaultTenantSlug)
                {
                    await _next(context);
                    return;
                }

                _logger.LogWarning("Tenant nao encontrado para subdomain '{Slug}' (host '{Host}')",
                    slug, context.Request.Host.Host);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync($"Tenant '{slug}' nao encontrado.");
                return;
            }

            if (tenant.TenStatus == TenantStatus.Suspended)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync($"Tenant '{tenant.TenSlug}' esta suspenso.");
                return;
            }

            tenantContext.Resolve(tenant.TenId, tenant.TenSlug, tenant.TenDatabaseFile);
            await _next(context);
        }

        /// <summary>
        /// Extrai o slug do subdomain. Casos:
        ///   acme.connectveiculos.dev.br        -> "acme"
        ///   www.connectveiculos.dev.br         -> "default"
        ///   connectveiculos.dev.br             -> "default"
        ///   136.248.77.154 (IP nu)             -> "default"
        ///   localhost                          -> "default" (dev)
        /// </summary>
        internal static string ExtractTenantSlug(string host)
        {
            if (string.IsNullOrEmpty(host)) return DefaultTenantSlug;

            // IP literal (v4 ou v6) — sempre tenant default
            if (System.Net.IPAddress.TryParse(host, out _)) return DefaultTenantSlug;
            if (host.StartsWith('[') && host.EndsWith(']')) return DefaultTenantSlug;

            // localhost ou domain sem ponto (dev)
            if (!host.Contains('.')) return DefaultTenantSlug;

            var parts = host.Split('.');
            // dominio raiz (X.Y.Z) — sem subdomain — eh default
            // Para connectveiculos.dev.br temos 3 partes (connectveiculos / dev / br) mas eh raiz.
            // Para www.connectveiculos.dev.br temos 4 partes (www / connectveiculos / dev / br) -> www
            // Heuristica: tudo que tiver mais que 3 partes tem subdomain; "www" -> default; outro -> tenant.
            if (parts.Length <= 3) return DefaultTenantSlug;

            var first = parts[0].ToLowerInvariant();
            return first == "www" ? DefaultTenantSlug : first;
        }
    }

    public static class TenantResolutionMiddlewareExtensions
    {
        public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
            => app.UseMiddleware<TenantResolutionMiddleware>();
    }
}
