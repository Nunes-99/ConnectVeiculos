using ConnectVeiculos.Application.Interfaces.Catalogo;
using ConnectVeiculos.Core.Catalogo;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Cache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller publico para consulta do catalogo de veiculos disponiveis
    /// </summary>
    /// <remarks>
    /// Este endpoint nao requer autenticacao e pode ser usado para exibir o catalogo
    /// de veiculos disponiveis para venda em websites publicos.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CatalogoController : ControllerBase
    {
        // Loja-modelo do sistema, nao e de nenhum cliente: fica fora do sitemap.
        private const string TenantModelo = "default";

        private readonly ICacheService _cacheService;
        private readonly ITenantContext _tenantContext;

        public CatalogoController(ICacheService cacheService, ITenantContext tenantContext)
        {
            _cacheService = cacheService;
            _tenantContext = tenantContext;
        }

        /// <summary>
        /// Consulta o catalogo de veiculos disponiveis para venda
        /// </summary>
        /// <param name="consultarCatalogoUseCase">Use case de consulta injetado</param>
        /// <param name="marca">Filtro por marca do veiculo</param>
        /// <param name="anoMin">Ano minimo de fabricacao</param>
        /// <param name="anoMax">Ano maximo de fabricacao</param>
        /// <param name="precoMin">Preco minimo</param>
        /// <param name="precoMax">Preco maximo</param>
        /// <returns>Lista de veiculos disponiveis com imagens</returns>
        /// <response code="200">Catalogo retornado com sucesso</response>
        /// <param name="lojaId">ID da loja para filtrar catalogo (opcional)</param>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        // Cache so no browser do cliente (private) para evitar que proxies/CDNs
        // intermediarios sirvam resposta de um tenant pra outro quando o tenant
        // e resolvido por header (e nao por subdomain/query).
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByQueryKeys = new[] { "tenant", "marca", "anoMin", "anoMax", "precoMin", "precoMax", "lojaId" })]
        public async Task<IActionResult> ConsultarCatalogo(
            [FromServices] IConsultarCatalogoUseCase consultarCatalogoUseCase,
            [FromQuery] string marca = "",
            [FromQuery] int? anoMin = null,
            [FromQuery] int? anoMax = null,
            [FromQuery] decimal? precoMin = null,
            [FromQuery] decimal? precoMax = null,
            [FromQuery] int? lojaId = null)
        {
            var cacheKey = $"{CacheKeys.Catalogo}_{lojaId}_{marca}_{anoMin}_{anoMax}_{precoMin}_{precoMax}";

            var cached = _cacheService.Get<object>(cacheKey);
            if (cached != null)
            {
                return Ok(cached);
            }

            var resultado = await consultarCatalogoUseCase.Execute(marca, anoMin, anoMax, precoMin, precoMax, lojaId);

            // Cache por 1 minuto (menor para refletir mudancas mais rapido)
            _cacheService.Set(cacheKey, resultado, TimeSpan.FromMinutes(1));

            return Ok(resultado);
        }

        /// <summary>
        /// Consulta o catalogo de veiculos por slug da loja
        /// </summary>
        [HttpGet("slug/{slug}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByQueryKeys = new[] { "tenant", "marca", "anoMin", "anoMax", "precoMin", "precoMax" })]
        public async Task<IActionResult> ConsultarCatalogoPorSlug(
            [FromServices] IConsultarCatalogoUseCase consultarCatalogoUseCase,
            [FromServices] ILojaRepository lojaRepository,
            string slug,
            [FromQuery] string marca = "",
            [FromQuery] int? anoMin = null,
            [FromQuery] int? anoMax = null,
            [FromQuery] decimal? precoMin = null,
            [FromQuery] decimal? precoMax = null)
        {
            var loja = await lojaRepository.GetBySlugAsync(slug);
            if (loja == null) return NotFound(new { message = "Loja não encontrada." });

            var cacheKey = $"{CacheKeys.Catalogo}_slug_{slug}_{marca}_{anoMin}_{anoMax}_{precoMin}_{precoMax}";

            var cached = _cacheService.Get<object>(cacheKey);
            if (cached != null)
            {
                return Ok(cached);
            }

            var resultado = await consultarCatalogoUseCase.Execute(marca, anoMin, anoMax, precoMin, precoMax, loja.LojId);

            _cacheService.Set(cacheKey, resultado, TimeSpan.FromMinutes(1));

            return Ok(resultado);
        }

        [HttpGet("veiculo/{veiculoId}")]
        public async Task<IActionResult> ConsultarVeiculo([FromServices] IConsultarCatalogoUseCase consultarCatalogoUseCase, int veiculoId)
        {
            var resultado = await consultarCatalogoUseCase.Execute("", null, null, null, null, null);
            var veiculo = resultado.Veiculos.FirstOrDefault(v => v.VeiId == veiculoId);
            if (veiculo == null) return NotFound();
            return Ok(veiculo);
        }

        [HttpGet("veiculo/{veiculoId}/qrcode")]
        public IActionResult GerarQrCodeUrl(int veiculoId, [FromQuery] int? lojaId = null)
        {
            // O segmento da rota e' o slug do TENANT — lojaId aqui gerava
            // /catalogo/3/veiculo/7, que nao casa com rota nenhuma.
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var slug = _tenantContext.IsResolved ? _tenantContext.TenantSlug : null;
            var url = CatalogoUrl.Veiculo(baseUrl, slug, veiculoId);
            return Ok(new { url, veiculoId, lojaId });
        }

        /// <summary>
        /// Lista os tenants publicos do SaaS — usado pelo sitemap.xml do SSR
        /// para gerar URLs de catalogo multi-tenant.
        ///
        /// "Publico" aqui e mais estreito que "ativo". Antes bastava estar ativo,
        /// e o resultado era que todo autocadastro entrava no sitemap sozinho e
        /// era oferecido ao Google como loja real do ConnectVeiculos — em
        /// 2026-09-15 havia quatro tenants la, entre eles um cadastro feito com
        /// e-mail temporario. Alem de nao ajudar ninguem, dilui a autoridade do
        /// dominio entre catalogos vazios ou de teste.
        ///
        /// Entram apenas lojas ativas, fora do plano gratuito e diferentes de
        /// "default", que e a loja-modelo do sistema. Quem tem catalogo vazio e
        /// descartado depois, no proprio sitemap, que ja consulta o catalogo de
        /// cada loja.
        /// </summary>
        [HttpGet("public-tenants")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> ListarTenantsPublicos([FromServices] ConnectVeiculos.Infrastructure.Database.EntityFramework.MasterDbContext master, CancellationToken ct)
        {
            var planosPublicos = master.Planos
                .Where(p => p.PlaNome != ConnectVeiculos.Core.Entities.Tenants.Plano.NomeGratuito)
                .Select(p => p.PlaId);

            var tenants = await master.Tenants
                .Where(t => t.TenStatus == ConnectVeiculos.Core.Entities.Tenants.TenantStatus.Active
                            && t.TenSlug != TenantModelo
                            && t.TenPlaId != null
                            && planosPublicos.Contains(t.TenPlaId.Value))
                .Select(t => new { slug = t.TenSlug, nome = t.TenNome })
                .ToListAsync(ct);
            return Ok(tenants);
        }
    }
}
