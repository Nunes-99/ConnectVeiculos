using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Services.Pagamentos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Assinatura mensal do plano no cartao (Mercado Pago). Ver
    /// <see cref="MercadoPagoAssinaturaService"/> para o fluxo completo.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AssinaturaController : ControllerBase
    {
        private readonly IAssinaturaService _assinaturas;
        private readonly ILogger<AssinaturaController> _logger;

        public AssinaturaController(IAssinaturaService assinaturas, ILogger<AssinaturaController> logger)
        {
            _assinaturas = assinaturas;
            _logger = logger;
        }

        public class CheckoutInput
        {
            public int PlanoId { get; set; }
            public string Email { get; set; } = string.Empty;
        }

        // Cria a assinatura e devolve a pagina do Mercado Pago onde o cliente
        // cadastra o cartao. O plano so muda quando o aviso confirmar.
        [HttpPost("checkout")]
        [Authorize(Roles = "Administrador,Gerente")]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Checkout(
            [FromBody] CheckoutInput input,
            [FromServices] ITenantContext tenantContext,
            CancellationToken ct)
        {
            if (!tenantContext.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            try
            {
                var link = await _assinaturas.IniciarAsync(tenantContext.TenantId, input.PlanoId, input.Email, ct);
                return Ok(new { initPoint = link });
            }
            catch (AssinaturaException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        [HttpPost("cancelar")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> Cancelar([FromServices] ITenantContext tenantContext, CancellationToken ct)
        {
            if (!tenantContext.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            try
            {
                await _assinaturas.CancelarAsync(tenantContext.TenantId, ct);
                return Ok(new { mensagem = "Assinatura cancelada. A loja voltou para o plano gratuito." });
            }
            catch (AssinaturaException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        // Aviso do Mercado Pago (configurado no painel deles, em Webhooks).
        // O id vem na query (?data.id=...&type=...) e/ou no corpo; o conteudo e'
        // relido na API deles, entao o corpo nao precisa ser confiavel, mas a
        // assinatura do cabecalho evita que qualquer um force releituras.
        [HttpPost("webhook/mercadopago")]
        [AllowAnonymous]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Webhook([FromServices] IOptions<MercadoPagoSettings> settings, CancellationToken ct)
        {
            string? tipo = Request.Query["type"].FirstOrDefault() ?? Request.Query["topic"].FirstOrDefault();
            string? id = Request.Query["data.id"].FirstOrDefault() ?? Request.Query["id"].FirstOrDefault();

            if (string.IsNullOrEmpty(tipo) || string.IsNullOrEmpty(id))
            {
                try
                {
                    using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
                    var raiz = doc.RootElement;
                    tipo ??= raiz.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (id == null && raiz.TryGetProperty("data", out var d) && d.TryGetProperty("id", out var i))
                        id = i.ValueKind == JsonValueKind.String ? i.GetString() : i.GetRawText();
                }
                catch (JsonException) { }
            }

            if (string.IsNullOrEmpty(tipo) || string.IsNullOrEmpty(id))
                return Ok(); // aviso que nao nos interessa

            if (!MercadoPagoAssinaturaService.AvisoAutentico(
                    Request.Headers["x-signature"].FirstOrDefault(),
                    Request.Headers["x-request-id"].FirstOrDefault(),
                    id,
                    settings.Value.WebhookSecret))
            {
                _logger.LogWarning("Aviso do Mercado Pago com assinatura invalida ({Tipo} {Id})", tipo, id);
                return Unauthorized();
            }

            await _assinaturas.ProcessarAvisoAsync(tipo, id, ct);
            return Ok();
        }
    }
}
