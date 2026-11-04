using System.Security.Claims;
using ConnectVeiculos.Core.Contratos;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Modelos de contrato e contratos dos veiculos (gerar, assinar pela loja,
    /// colher a assinatura do cliente na loja, enviar o link, PDF). O cliente
    /// assina pelo link em <see cref="AssinarController"/>.
    /// </summary>
    [ApiController]
    [Route("api/contratos")]
    [Authorize(Roles = "Administrador,Gerente,Vendedor")]
    [EnableRateLimiting("api")]
    public class ContratosController : ControllerBase
    {
        private readonly IContratoService _contratos;

        public ContratosController(IContratoService contratos)
        {
            _contratos = contratos;
        }

        // ---------- Modelos ----------

        [HttpGet("modelos")]
        public async Task<IActionResult> Modelos([FromQuery] bool todos, CancellationToken ct)
            => Ok(await _contratos.ListarModelosAsync(todos, ct));

        [HttpPost("modelos")]
        [Authorize(Roles = "Administrador,Gerente")]
        public Task<IActionResult> CriarModelo([FromBody] ModeloContratoInput input, CancellationToken ct)
            => Executar(() => _contratos.SalvarModeloAsync(null, input, ct));

        [HttpPut("modelos/{id:int}")]
        [Authorize(Roles = "Administrador,Gerente")]
        public Task<IActionResult> SalvarModelo(int id, [FromBody] ModeloContratoInput input, CancellationToken ct)
            => Executar(() => _contratos.SalvarModeloAsync(id, input, ct));

        [HttpDelete("modelos/{id:int}")]
        [Authorize(Roles = "Administrador,Gerente")]
        public Task<IActionResult> ExcluirModelo(int id, CancellationToken ct)
            => Executar(async () => { await _contratos.ExcluirModeloAsync(id, ct); return new { ok = true }; });

        [HttpGet("variaveis")]
        public IActionResult Variaveis() => Ok(_contratos.Variaveis());

        [HttpPost("modelos/preview")]
        public Task<IActionResult> PreviewModelo([FromBody] ModeloPreviewInput input, CancellationToken ct)
            => Executar(() => _contratos.PreviewModeloAsync(input, ct));

        // ---------- Contratos ----------

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] ContratoFiltro filtro, CancellationToken ct)
            => Ok(await _contratos.ListarAsync(filtro, ct));

        [HttpGet("sugestao")]
        public Task<IActionResult> Sugestao([FromQuery] int veiculoId, [FromQuery] int modeloId, CancellationToken ct)
            => Executar(() => _contratos.SugerirAsync(veiculoId, modeloId, ct));

        [HttpPost("preview")]
        public Task<IActionResult> Preview([FromBody] ContratoGerarInput input, CancellationToken ct)
            => Executar(() => _contratos.PreviewAsync(input, ct));

        [HttpPost]
        public Task<IActionResult> Gerar([FromBody] ContratoGerarInput input, CancellationToken ct)
            => Executar(() => _contratos.GerarAsync(input, UsuarioId(), ct));

        [HttpGet("{id:int}")]
        public Task<IActionResult> Obter(int id, CancellationToken ct) => Executar(() => _contratos.ObterAsync(id, ct));

        [HttpGet("{id:int}/texto")]
        public Task<IActionResult> Texto(int id, CancellationToken ct) => Executar(() => _contratos.TextoAsync(id, ct));

        [HttpPost("{id:int}/cancelar")]
        [Authorize(Roles = "Administrador,Gerente")]
        public Task<IActionResult> Cancelar(int id, CancellationToken ct) => Executar(() => _contratos.CancelarAsync(id, ct));

        [HttpPost("{id:int}/assinar-loja")]
        [Authorize(Roles = "Administrador,Gerente")]
        public Task<IActionResult> AssinarLoja(int id, [FromBody] AssinaturaInput input, CancellationToken ct)
            => Executar(() => _contratos.AssinarPelaLojaAsync(id, input, UsuarioId() ?? 0, UsuarioNome(), Origem(), ct));

        [HttpPost("{id:int}/assinar-presencial")]
        public Task<IActionResult> AssinarPresencial(int id, [FromBody] AssinaturaInput input, CancellationToken ct)
            => Executar(() => _contratos.AssinarPresencialAsync(id, input, UsuarioId() ?? 0, UsuarioNome(), Origem(), ct));

        public class EnvioInput
        {
            /// <summary>Endereco do sistema que o usuario esta usando (o link aponta para ele).</summary>
            public string UrlBase { get; set; } = string.Empty;
        }

        [HttpPost("{id:int}/enviar-email")]
        public Task<IActionResult> EnviarEmail(int id, [FromBody] EnvioInput input, CancellationToken ct)
            => Executar(async () =>
            {
                await _contratos.EnviarLinkPorEmailAsync(id, UrlBaseValida(input.UrlBase), ct);
                return new { ok = true };
            });

        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> Pdf(int id, CancellationToken ct)
        {
            try
            {
                var (pdf, nome) = await _contratos.PdfAsync(id, ct);
                return File(pdf, "application/pdf", nome);
            }
            catch (ContratoException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        /// <summary>
        /// O link do e-mail aponta para o endereco em que o usuario esta, desde
        /// que seja um dos enderecos do sistema (nunca um site qualquer).
        /// </summary>
        private string UrlBaseValida(string? urlBase)
        {
            const string padrao = "https://connectveiculos.dev.br";
            if (!Uri.TryCreate(urlBase, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return padrao;
            var origem = uri.GetLeftPart(UriPartial.Authority);
            var host = uri.Host.ToLowerInvariant();
            var raizes = (Environment.GetEnvironmentVariable("ALLOWED_ROOT_DOMAINS") ?? "connectveiculos.dev.br,connectveiculos.com.br")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var explicitas = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "http://localhost:4200")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var permitida = explicitas.Any(o => string.Equals(o.TrimEnd('/'), origem, StringComparison.OrdinalIgnoreCase))
                || (uri.Scheme == "https" && raizes.Any(r => host == r || host.EndsWith("." + r)));
            return permitida ? origem : padrao;
        }

        private int? UsuarioId()
        {
            var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value;
            return int.TryParse(v, out var id) ? id : null;
        }

        private string UsuarioNome() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "usuário";

        private AssinaturaOrigem Origem() => new()
        {
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Navegador = Request.Headers.UserAgent.ToString()
        };

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
