using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.Napista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// NaPista: a gerencia conecta a conta da loja (OAuth, mesmo usuario e senha
    /// do NaPista), escolhe a loja e anuncia os veiculos; os leads do NaPista
    /// entram na lista de Leads. Anunciar mexe no estoque publico da loja, por
    /// isso so Administrador e Gerente.
    /// </summary>
    [ApiController]
    [Route("api/integracoes/napista")]
    [Authorize(Roles = Gestao)]
    [EnableRateLimiting("api")]
    public class NapistaController : ControllerBase
    {
        private const string Gestao = "Administrador,Gerente";
        private readonly INapistaService _napista;

        public NapistaController(INapistaService napista)
        {
            _napista = napista;
        }

        // ---------------- Conexao ----------------

        [HttpGet]
        public Task<IActionResult> Status(CancellationToken ct) => Executar(() => _napista.StatusAsync(ct));

        [HttpGet("autorizar")]
        public Task<IActionResult> Autorizar() => Executar(() => Task.FromResult(new { url = _napista.UrlAutorizacao() }));

        public class ConexaoInput
        {
            public string Code { get; set; } = string.Empty;
            public string? State { get; set; }
        }

        [HttpPost("conectar")]
        public Task<IActionResult> Conectar([FromBody] ConexaoInput input, CancellationToken ct)
            => Executar(() => _napista.ConectarAsync(input.Code, input.State, ct));

        /// <summary>
        /// Volta do login do NaPista sem exigir sessao neste dominio (mesma regra
        /// da Credere): a redirect_uri cadastrada e' a do connectveiculos.dev.br,
        /// mas a loja pode ter clicado em Conectar pelo endereco dela. O state
        /// cifrado (10 min) diz de qual loja e'; se caiu em outra, devolve o slug
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
                await _napista.ConectarAsync(input.Code, input.State, ct);
                return new { conectado = true, tenant = slug };
            });
        }

        public class LojaInput
        {
            public string SellerId { get; set; } = string.Empty;
        }

        [HttpPut("loja")]
        public Task<IActionResult> Loja([FromBody] LojaInput input, CancellationToken ct)
            => Executar(() => _napista.EscolherLojaAsync(input.SellerId, ct));

        [HttpDelete]
        public Task<IActionResult> Desconectar(CancellationToken ct)
            => Executar(async () => { await _napista.DesconectarAsync(ct); return new { ok = true }; });

        // ---------------- Catalogo ----------------

        [HttpGet("catalogo/{categoria}/marcas")]
        public Task<IActionResult> Marcas(string categoria, CancellationToken ct)
            => Executar(() => _napista.MarcasAsync(categoria, ct));

        [HttpGet("catalogo/{categoria}/marcas/{marcaId}/modelos")]
        public Task<IActionResult> Modelos(string categoria, string marcaId, [FromQuery] int? ano, CancellationToken ct)
            => Executar(() => _napista.ModelosAsync(categoria, marcaId, ano, ct));

        [HttpGet("catalogo/{categoria}/modelos/{modeloId}/versoes")]
        public Task<IActionResult> Versoes(string categoria, string modeloId, [FromQuery] int? ano, CancellationToken ct)
            => Executar(() => _napista.VersoesAsync(categoria, modeloId, ano, ct));

        [HttpGet("catalogo/{categoria}/atributos")]
        public Task<IActionResult> Atributos(string categoria, CancellationToken ct)
            => Executar(() => _napista.AtributosAsync(categoria, ct));

        // ---------------- Anuncios ----------------

        [HttpGet("veiculos/{veiculoId:int}")]
        public Task<IActionResult> Veiculo(int veiculoId, CancellationToken ct)
            => Executar(() => _napista.ObterVeiculoAsync(veiculoId, ct));

        [HttpPost("veiculos/{veiculoId:int}")]
        public Task<IActionResult> Publicar(int veiculoId, [FromBody] NapistaAnuncioDados dados, CancellationToken ct)
            => Executar(() => _napista.PublicarAsync(veiculoId, dados, ct));

        [HttpPost("veiculos/{veiculoId:int}/pausar")]
        public Task<IActionResult> Pausar(int veiculoId, CancellationToken ct)
            => Executar(() => _napista.PausarAsync(veiculoId, ct));

        // ---------------- Leads ----------------

        [HttpPost("leads/importar")]
        public Task<IActionResult> ImportarLeads([FromQuery] int dias = 30, CancellationToken ct = default)
            => Executar(() => _napista.ImportarLeadsAsync(dias, ct));

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (NapistaException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }
}
