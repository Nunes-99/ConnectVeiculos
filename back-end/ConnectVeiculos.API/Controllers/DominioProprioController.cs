using System.Net;
using ConnectVeiculos.Core.Catalogo;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Dominio proprio do catalogo, configurado pelo administrador da loja na
    /// tela de Integracoes. Fluxo:
    ///   1. a loja grava o dominio aqui (fica "aguardando");
    ///   2. aponta o DNS (registro A) para o IP do servidor;
    ///   3. o scripts/dominios-proprios.sh, de 5 em 5 minutos na VM, ve o DNS
    ///      certo, gera o certificado, configura o nginx e ativa o dominio pelo
    ///      TenantsAdminController — a partir dai os links publicos usam o dominio.
    /// </summary>
    [ApiController]
    [Route("api/integracoes/dominio-proprio")]
    [Authorize(Roles = "Administrador,Gerente")]
    public sealed class DominioProprioController : ControllerBase
    {
        private readonly ITenantContext _tenantContext;
        private readonly ITenantStore _store;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DominioProprioController> _logger;

        public DominioProprioController(
            ITenantContext tenantContext,
            ITenantStore store,
            IConfiguration configuration,
            ILogger<DominioProprioController> logger)
        {
            _tenantContext = tenantContext;
            _store = store;
            _configuration = configuration;
            _logger = logger;
        }

        public sealed class DominioRequest
        {
            public string? Dominio { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> Obter(CancellationToken ct)
        {
            if (!_tenantContext.IsResolved) return BadRequest(new { error = "Tenant nao resolvido." });
            var tenant = await _store.GetByIdAsync(_tenantContext.TenantId, ct);
            if (tenant == null) return NotFound();

            var ip = IpServidor();
            var dominio = tenant.TenDominioProprio;
            return Ok(new
            {
                dominio,
                ativo = tenant.DominioProprioAtivo(),
                ativoEm = tenant.TenDominioAtivoEm,
                ipServidor = ip,
                dnsAponta = dominio != null && await ApontaParaAsync(dominio, ip, ct),
                wwwAponta = dominio != null && await ApontaParaAsync("www." + dominio, ip, ct),
                // No dominio da loja o catalogo fica na raiz (server.ts do SSR).
                urlCatalogo = dominio != null && tenant.DominioProprioAtivo()
                    ? DominioProprio.UrlBase(dominio) + "/"
                    : null,
            });
        }

        [HttpPut]
        public async Task<IActionResult> Salvar(
            [FromBody] DominioRequest request,
            [FromServices] ConnectVeiculosDbContext ctx,
            CancellationToken ct)
        {
            if (!_tenantContext.IsResolved) return BadRequest(new { error = "Tenant nao resolvido." });

            var dominio = DominioProprio.Normalizar(request.Dominio);
            var anterior = await _store.GetByIdAsync(_tenantContext.TenantId, ct);
            var estavaAtivo = anterior?.DominioProprioAtivo() == true;

            // null = remover, sempre permitido. Dominio novo so entra valido e
            // ja apontando para o servidor; o atual nao se troca direto.
            if (dominio != null)
            {
                var ip = IpServidor();
                var aponta = DominioProprio.EhValido(dominio) && await ApontaParaAsync(dominio, ip, ct);
                var motivo = DominioProprio.MotivoParaRecusar(anterior?.TenDominioProprio, dominio, aponta, ip);
                if (motivo != null)
                    return anterior?.TenDominioProprio != null && anterior.TenDominioProprio != dominio
                        ? Conflict(new { error = motivo })
                        : BadRequest(new { error = motivo });
                if (dominio == anterior?.TenDominioProprio)
                    return Ok(new { dominio, mensagem = "Esse domínio já está cadastrado." });
            }

            try
            {
                await _store.UpdateDominioProprioAsync(_tenantContext.TenantId, dominio, ct);
            }
            catch (InvalidOperationException)
            {
                return Conflict(new { error = "Esse domínio já está cadastrado em outra loja." });
            }

            // Os links publicos so mudam quando o servidor confirmar o novo
            // dominio; ate la (ou sem dominio) voltam para o endereco da plataforma.
            if (estavaAtivo && anterior!.TenDominioProprio != dominio)
                await DominioProprioLojas.AplicarUrlCatalogoAsync(ctx, UrlPlataforma(), ct);

            _logger.LogInformation("Dominio proprio do tenant {Slug}: '{Anterior}' -> '{Novo}'",
                _tenantContext.TenantSlug, anterior?.TenDominioProprio, dominio);

            return Ok(new
            {
                dominio,
                mensagem = dominio == null
                    ? "Domínio próprio removido. O catálogo segue no endereço da plataforma."
                    : $"Domínio {dominio} cadastrado. O certificado de segurança (HTTPS) é gerado sozinho em até 10 minutos e o domínio fica Ativo."
            });
        }

        private string IpServidor()
            => _configuration["DominioProprio:IpServidor"] ?? "136.248.77.154";

        private string UrlPlataforma()
            => (_configuration["FacebookCatalogSettings:PublicSiteUrl"] is { Length: > 0 } url ? url : "https://connectveiculos.dev.br").TrimEnd('/');

        private static async Task<bool> ApontaParaAsync(string host, string ip, CancellationToken ct)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                var enderecos = await Dns.GetHostAddressesAsync(host, timeout.Token);
                return enderecos.Any(a => a.ToString() == ip);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Dominio que ainda nao existe no DNS, timeout etc.: so "nao aponta".
                return false;
            }
        }
    }
}
