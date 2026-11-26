using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.Olx;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// OLX: a gerencia conecta a conta da loja (OAuth, usuario da OLX com plano
    /// profissional) e anuncia os carros. Mexe no estoque publico da loja, por
    /// isso so Administrador e Gerente.
    /// </summary>
    [ApiController]
    [Route("api/integracoes/olx")]
    [Authorize(Roles = Gestao)]
    [EnableRateLimiting("api")]
    public class OlxController : ControllerBase
    {
        private const string Gestao = "Administrador,Gerente";
        private readonly IOlxService _olx;

        public OlxController(IOlxService olx)
        {
            _olx = olx;
        }

        // ---------------- Conexao ----------------

        [HttpGet]
        public Task<IActionResult> Status(CancellationToken ct) => Executar(() => _olx.StatusAsync(ct));

        [HttpGet("autorizar")]
        public Task<IActionResult> Autorizar() => Executar(() => Task.FromResult(new { url = _olx.UrlAutorizacao() }));

        public class ConexaoInput
        {
            public string Code { get; set; } = string.Empty;
            public string? State { get; set; }
        }

        [HttpPost("conectar")]
        public Task<IActionResult> Conectar([FromBody] ConexaoInput input, CancellationToken ct)
            => Executar(() => _olx.ConectarAsync(input.Code, input.State, ct));

        /// <summary>
        /// Volta do login da OLX sem exigir sessao neste dominio (mesma regra do
        /// NaPista): a redirect_uri cadastrada e' a do connectveiculos.dev.br.
        /// O state cifrado diz de qual loja e'; se caiu em outra, devolve o slug
        /// para o front repetir com ?tenant=.
        /// </summary>
        [HttpPost("retorno")]
        [AllowAnonymous]
        [EnableRateLimiting("public")]
        public async Task<IActionResult> Retorno([FromBody] ConexaoInput input, [FromServices] IOAuthStateProtector state,
            [FromServices] ITenantContext tenant, CancellationToken ct)
        {
            string slug;
            try
            {
                slug = state.Decifrar(input.State).TenantSlug;
            }
            catch (OAuthStateException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
            var atual = tenant.IsResolved ? tenant.TenantSlug : "default";
            if (!string.Equals(slug, atual, StringComparison.OrdinalIgnoreCase))
                return Ok(new { conectado = false, tenant = slug });
            return await Executar(async () =>
            {
                await _olx.ConectarAsync(input.Code, input.State, ct);
                return new { conectado = true, tenant = slug };
            });
        }

        [HttpDelete]
        public Task<IActionResult> Desconectar(CancellationToken ct)
            => Executar(async () => { await _olx.DesconectarAsync(ct); return new { ok = true }; });

        // ---------------- Catalogo ----------------

        [HttpGet("catalogo/marcas")]
        public Task<IActionResult> Marcas(CancellationToken ct) => Executar(() => _olx.MarcasAsync(ct));

        [HttpGet("catalogo/marcas/{marcaId}/modelos")]
        public Task<IActionResult> Modelos(string marcaId, CancellationToken ct)
            => Executar(() => _olx.ModelosAsync(marcaId, ct));

        [HttpGet("catalogo/marcas/{marcaId}/modelos/{modeloId}/versoes")]
        public Task<IActionResult> Versoes(string marcaId, string modeloId, CancellationToken ct)
            => Executar(() => _olx.VersoesAsync(marcaId, modeloId, ct));

        [HttpGet("catalogo/atributos")]
        public IActionResult Atributos() => Ok(_olx.Atributos());

        // ---------------- Anuncios ----------------

        [HttpGet("veiculos/{veiculoId:int}")]
        public Task<IActionResult> Veiculo(int veiculoId, CancellationToken ct)
            => Executar(() => _olx.ObterVeiculoAsync(veiculoId, ct));

        [HttpPost("veiculos/{veiculoId:int}")]
        public Task<IActionResult> Publicar(int veiculoId, [FromBody] OlxAnuncioDados dados, CancellationToken ct)
            => Executar(() => _olx.PublicarAsync(veiculoId, dados, ct));

        [HttpPost("veiculos/{veiculoId:int}/pausar")]
        public Task<IActionResult> Pausar(int veiculoId, CancellationToken ct)
            => Executar(() => _olx.PausarAsync(veiculoId, ct));

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (OlxException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }
}
