using System.Security.Claims;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Services.ConsultaPlaca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Consulta veicular por placa (ZapCar) paga com creditos da loja. Quem
    /// avalia carro (gestao e vendedor) consulta; so a gestao compra creditos.
    /// </summary>
    [ApiController]
    [Route("api/consulta-placa")]
    [Authorize(Roles = "Administrador,Gerente,Vendedor,Visualizador")]
    public class ConsultaPlacaController : ControllerBase
    {
        private const string Consultam = "Administrador,Gerente,Vendedor";
        private const string Gestao = "Administrador,Gerente";

        private readonly ConsultaPlacaService _consultas;
        private readonly ITenantContext _tenant;

        public ConsultaPlacaController(ConsultaPlacaService consultas, ITenantContext tenant)
        {
            _consultas = consultas;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<IActionResult> Painel(CancellationToken ct)
        {
            if (!_tenant.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            return Ok(await _consultas.PainelAsync(_tenant.TenantId, ct));
        }

        public class ConsultaInput
        {
            public string Placa { get; set; } = "";
            public string Servico { get; set; } = "";
        }

        [HttpPost]
        [Authorize(Roles = Consultam)]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Consultar([FromBody] ConsultaInput input, CancellationToken ct)
        {
            if (!_tenant.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            var usuarioId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value, out var id) ? id : 0;
            try
            {
                return Ok(await _consultas.ConsultarAsync(_tenant.TenantId, input.Placa, input.Servico, usuarioId, ct));
            }
            catch (DomainException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obter(int id, CancellationToken ct)
        {
            if (!_tenant.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            var item = await _consultas.ObterAsync(_tenant.TenantId, id, ct);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpGet("{id:int}/documento")]
        public async Task<IActionResult> Documento(int id, CancellationToken ct)
        {
            var doc = await _consultas.DocumentoAsync(id, ct);
            if (doc == null) return NotFound(new { error = "O documento desta consulta não está disponível." });
            var extensao = doc.Value.Tipo.Contains("pdf") ? "pdf" : doc.Value.Tipo.Contains("png") ? "png" : "jpg";
            return File(doc.Value.Conteudo, doc.Value.Tipo, $"consulta-{id}.{extensao}");
        }

        public class CreditosInput
        {
            public decimal Valor { get; set; }
            public string Email { get; set; } = "";
        }

        [HttpPost("creditos/pix")]
        [Authorize(Roles = Gestao)]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> ComprarCreditos(
            [FromBody] CreditosInput input,
            [FromServices] IAssinaturaService pagamentos,
            [FromServices] IOptions<ZapCarSettings> settings,
            CancellationToken ct)
        {
            if (!_tenant.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            if (!settings.Value.ValoresPacotes().Contains(input.Valor))
                return UnprocessableEntity(new { error = "Escolha um dos valores de créditos." });
            try
            {
                return Ok(await pagamentos.GerarPixCreditosAsync(_tenant.TenantId, input.Valor, input.Email, ct));
            }
            catch (AssinaturaException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }
}
