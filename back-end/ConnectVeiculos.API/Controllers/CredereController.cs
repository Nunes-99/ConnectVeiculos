using System.Security.Claims;
using ConnectVeiculos.Core.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Credere (financiamento multibanco): a gerencia conecta a conta da loja
    /// (OAuth) e escolhe a loja; qualquer vendedor simula a ficha dele.
    /// Fica sob /api/integracoes para o cookie do state do OAuth valer aqui.
    /// </summary>
    [ApiController]
    [Route("api/integracoes/credere")]
    [Authorize]
    [EnableRateLimiting("api")]
    public class CredereController : ControllerBase
    {
        private const string Gestao = "Administrador,Gerente";
        private readonly ICredereService _credere;

        public CredereController(ICredereService credere)
        {
            _credere = credere;
        }

        [HttpGet]
        public Task<IActionResult> Status(CancellationToken ct) => Executar(() => _credere.StatusAsync(ct));

        [HttpGet("autorizar")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Autorizar()
            => Executar(() => Task.FromResult(new { url = _credere.UrlAutorizacao() }));

        public class ConexaoInput
        {
            public string Code { get; set; } = string.Empty;
            public string? State { get; set; }
        }

        [HttpPost("conectar")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Conectar([FromBody] ConexaoInput input, CancellationToken ct)
            => Executar(() => _credere.ConectarAsync(input.Code, input.State, ct));

        public class LojaInput
        {
            public int LojaId { get; set; }
        }

        [HttpPut("loja")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Loja([FromBody] LojaInput input, CancellationToken ct)
            => Executar(() => _credere.EscolherLojaAsync(input.LojaId, ct));

        [HttpDelete]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Desconectar(CancellationToken ct)
            => Executar(async () => { await _credere.DesconectarAsync(ct); return new { ok = true }; });

        [HttpGet("modelos")]
        public Task<IActionResult> Modelos([FromQuery] string busca, [FromQuery] int? ano, CancellationToken ct)
            => Executar(() => _credere.BuscarModelosAsync(busca, ano, ct));

        [HttpGet("fichas/{fichaId:int}/campos")]
        public Task<IActionResult> Campos(int fichaId, CancellationToken ct)
            => Executar(() => _credere.CamposExigidosAsync(fichaId, SoDoVendedor(), ct));

        [HttpPost("fichas/{fichaId:int}/simular")]
        public Task<IActionResult> Simular(int fichaId, [FromBody] CredereSimulacaoInput input, CancellationToken ct)
            => Executar(() => _credere.SimularAsync(fichaId, input, SoDoVendedor(), User.FindFirst(ClaimTypes.Email)?.Value, ct));

        [HttpGet("fichas/{fichaId:int}/simulacao")]
        public Task<IActionResult> Simulacao(int fichaId, CancellationToken ct)
            => Executar(() => _credere.ObterSimulacaoAsync(fichaId, SoDoVendedor(), ct));

        /// <summary>Gestao ve todas as fichas; os demais, so as proprias.</summary>
        private int? SoDoVendedor()
            => User.IsInRole("Administrador") || User.IsInRole("Gerente")
                ? null
                : int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (CredereException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }
}
