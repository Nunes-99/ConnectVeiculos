using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Database.Interceptors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Endpoints administrativos para provisionamento de tenants. NAO usa o JWT
    /// dos usuarios — autentica via header X-Admin-Token comparado a env var
    /// ADMIN_API_TOKEN (configurado no .env da VM).
    ///
    /// Esses endpoints sao chamados pelo script scripts/criar-tenant.sh; humanos
    /// nao deveriam usar diretamente. Toda chamada eh logada.
    /// </summary>
    [ApiController]
    [Route("api/admin/tenants")]
    [Produces("application/json")]
    public sealed class TenantsAdminController : ControllerBase
    {
        private static readonly Regex SlugRegex = new("^[a-z][a-z0-9-]{2,30}$", RegexOptions.Compiled);

        private readonly MasterDbContext _master;
        private readonly ITenantStore _store;
        private readonly ITenantConnectionFactory _connFactory;
        private readonly SoftDeleteInterceptor _softDeleteInterceptor;
        private readonly ILogger<TenantsAdminController> _logger;
        private readonly string? _adminToken;

        public TenantsAdminController(
            MasterDbContext master,
            ITenantStore store,
            ITenantConnectionFactory connFactory,
            SoftDeleteInterceptor softDeleteInterceptor,
            IConfiguration configuration,
            ILogger<TenantsAdminController> logger)
        {
            _master = master;
            _store = store;
            _connFactory = connFactory;
            _softDeleteInterceptor = softDeleteInterceptor;
            _logger = logger;
            _adminToken = Environment.GetEnvironmentVariable("ADMIN_API_TOKEN")
                ?? configuration["AdminApi:Token"];
        }

        public sealed class CreateTenantRequest
        {
            public string Slug { get; set; } = string.Empty;
            public string Nome { get; set; } = string.Empty;
            public string AdminEmail { get; set; } = string.Empty;
            public string AdminSenha { get; set; } = string.Empty;
            public string? AdminNome { get; set; }
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateTenantRequest req, CancellationToken ct)
        {
            // 1) Auth
            if (string.IsNullOrEmpty(_adminToken))
                return StatusCode(503, new { message = "ADMIN_API_TOKEN nao configurado no servidor." });
            if (!Request.Headers.TryGetValue("X-Admin-Token", out var sent) || sent != _adminToken)
                return Unauthorized(new { message = "Token administrativo invalido ou ausente." });

            // 2) Validacao
            if (req == null || string.IsNullOrWhiteSpace(req.Slug) || string.IsNullOrWhiteSpace(req.Nome)
                || string.IsNullOrWhiteSpace(req.AdminEmail) || string.IsNullOrWhiteSpace(req.AdminSenha))
                return BadRequest(new { message = "Slug, nome, AdminEmail e AdminSenha sao obrigatorios." });

            var slug = req.Slug.Trim().ToLowerInvariant();
            if (!SlugRegex.IsMatch(slug))
                return BadRequest(new { message = "Slug invalido. Use 3-31 caracteres minusculos, comecando com letra (ex: 'acme', 'minha-loja')." });

            if (req.AdminSenha.Length < 6)
                return BadRequest(new { message = "Senha do admin deve ter no minimo 6 caracteres." });

            // 3) Conflito?
            var existente = await _master.Tenants.FirstOrDefaultAsync(t => t.TenSlug == slug, ct);
            if (existente != null)
                return Conflict(new { message = $"Tenant '{slug}' ja existe (id {existente.TenId})." });

            // 4) Cria registro no master
            var tenant = new Tenant(slug, req.Nome.Trim());
            _master.Tenants.Add(tenant);
            await _master.SaveChangesAsync(ct);

            _logger.LogInformation("Tenant '{Slug}' (id {Id}) criado no master", tenant.TenSlug, tenant.TenId);

            // 5) Cria o banco do tenant + schema + admin user.
            var connStr = _connFactory.GetConnectionStringForTenant(tenant.TenSlug, tenant.TenDatabaseFile);
            var optionsBuilder = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                .UseSqlite(connStr)
                .AddInterceptors(_softDeleteInterceptor);

            using (var ctx = new ConnectVeiculosDbContext(optionsBuilder.Options))
            {
                await ctx.Database.EnsureCreatedAsync(ct);

                var senhaHash = BCrypt.Net.BCrypt.HashPassword(req.AdminSenha);
                var admin = new Usuario(
                    usuId: 0,
                    usuNome: req.AdminNome?.Trim() ?? "Administrador",
                    usuCPF: string.Empty,
                    usuRG: string.Empty,
                    usuEmail: req.AdminEmail.Trim().ToLowerInvariant(),
                    usuSenha: senhaHash,
                    usuFuncao: "Administrador",
                    usuSts: true);
                ctx.Usuarios.Add(admin);
                await ctx.SaveChangesAsync(ct);
            }

            // 6) Invalida cache para o middleware passar a reconhecer o tenant
            _store.InvalidateCache();

            _logger.LogInformation("Tenant '{Slug}': banco {File} criado, admin {Email} seedado",
                tenant.TenSlug, tenant.TenDatabaseFile, req.AdminEmail);

            return Created($"/api/admin/tenants/{tenant.TenId}", new
            {
                tenantId = tenant.TenId,
                slug = tenant.TenSlug,
                nome = tenant.TenNome,
                databaseFile = tenant.TenDatabaseFile,
                mensagem = $"Tenant '{slug}' criado com sucesso. Acesse https://{slug}.connectveiculos.dev.br apos configurar DNS e cert SSL."
            });
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_adminToken))
                return StatusCode(503, new { message = "ADMIN_API_TOKEN nao configurado." });
            if (!Request.Headers.TryGetValue("X-Admin-Token", out var sent) || sent != _adminToken)
                return Unauthorized(new { message = "Token administrativo invalido ou ausente." });

            var tenants = await _master.Tenants.AsNoTracking().OrderBy(t => t.TenId).ToListAsync(ct);
            return Ok(tenants.Select(t => new
            {
                tenantId = t.TenId,
                slug = t.TenSlug,
                nome = t.TenNome,
                databaseFile = t.TenDatabaseFile,
                status = t.TenStatus.ToString(),
                criadoEm = t.TenDtCriacao
            }));
        }
    }
}
