using ConnectVeiculos.Core.Contratos;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Link publico de assinatura (o cliente nao tem login). A loja vem por
    /// ?tenant=slug; o token aleatorio do link identifica quem assina.
    /// </summary>
    [ApiController]
    [Route("api/assinar")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public class AssinarController : ControllerBase
    {
        private readonly IContratoService _contratos;

        public AssinarController(IContratoService contratos)
        {
            _contratos = contratos;
        }

        [HttpGet("{token}")]
        public Task<IActionResult> Abrir(string token, CancellationToken ct) => Executar(() => _contratos.AbrirLinkAsync(token, ct));

        [HttpPost("{token}/codigo")]
        public Task<IActionResult> Codigo(string token, CancellationToken ct)
            => Executar(async () => new { email = await _contratos.EnviarCodigoAsync(token, ct) });

        [HttpPost("{token}")]
        public Task<IActionResult> Assinar(string token, [FromBody] AssinaturaInput input, CancellationToken ct)
            => Executar(() => _contratos.AssinarPeloLinkAsync(token, input, new AssinaturaOrigem
            {
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Navegador = Request.Headers.UserAgent.ToString()
            }, ct));

        [HttpGet("{token}/pdf")]
        public async Task<IActionResult> Pdf(string token, CancellationToken ct)
        {
            try
            {
                var (pdf, nome) = await _contratos.PdfPeloLinkAsync(token, ct);
                return File(pdf, "application/pdf", nome);
            }
            catch (ContratoException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (ContratoException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }
}
