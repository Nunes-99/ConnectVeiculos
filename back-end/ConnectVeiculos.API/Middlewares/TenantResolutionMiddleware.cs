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

            // Override por query string (?tenant=slug). Usado pelas paginas
            // publicas (catalogo) onde o tenant vai como parametro de URL ao
            // inves de subdomain. Vence header + subdomain.
            var querySlug = context.Request.Query["tenant"].ToString();
            if (!string.IsNullOrWhiteSpace(querySlug))
            {
                slug = querySlug.Trim().ToLowerInvariant();
            }
            // Override por header explicito (X-Tenant-Slug). Util para:
            //  - desenvolvimento local em http://localhost sem DNS wildcard
            //  - clientes auto-cadastrados antes do DNS wildcard estar configurado
            // Seguranca: o header so altera roteamento. Endpoints protegidos
            // continuam exigindo JWT valido. JWT forjado e rejeitado pelo auth
            // middleware (assinado com secret), entao nao ha leak de dados.
            else if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var headerSlug)
                && !string.IsNullOrWhiteSpace(headerSlug))
            {
                slug = headerSlug.ToString().Trim().ToLowerInvariant();
            }

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
                 // JSON em vez de text/plain — frontend faz JSON.parse no response e
                 // texto cru quebra com 'Unexpected token T' na UI.
                 context.Response.StatusCode = StatusCodes.Status404NotFound;
                 context.Response.ContentType = "application/json";
                 await context.Response.WriteAsync(
                     $"{{\"error\":\"tenant_nao_encontrado\",\"mensagem\":\"Tenant '{slug}' nao encontrado.\",\"slug\":\"{slug}\"}}");
                return;
            }

            if (tenant.TenStatus == TenantStatus.Suspended)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                 context.Response.ContentType = "application/json";
                 await context.Response.WriteAsync(
                     $"{{\"error\":\"tenant_suspenso\",\"mensagem\":\"Tenant '{tenant.TenSlug}' esta suspenso.\",\"slug\":\"{tenant.TenSlug}\"}}");
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
        /// Public para permitir testes unitarios.
        /// </summary>
        public static string ExtractTenantSlug(string host)
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
