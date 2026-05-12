using ConnectVeiculos.Application.Interfaces.Catalogo;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
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
        private readonly ICacheService _cacheService;

        public CatalogoController(ICacheService cacheService)
        {
            _cacheService = cacheService;
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
            if (loja == null) return NotFound(new { message = "Loja nao encontrada." });

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
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var url = lojaId.HasValue ? $"{baseUrl}/catalogo/{lojaId}/veiculo/{veiculoId}" : $"{baseUrl}/catalogo?veiculo={veiculoId}";
            return Ok(new { url, veiculoId, lojaId });
        }

        /// <summary>
        /// Lista os tenants publicos do SaaS — usado pelo sitemap.xml do SSR
        /// para gerar URLs de catalogo multi-tenant.
        /// </summary>
        [HttpGet("public-tenants")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> ListarTenantsPublicos([FromServices] ConnectVeiculos.Infrastructure.Database.EntityFramework.MasterDbContext master, CancellationToken ct)
        {
            var tenants = await master.Tenants
                .Where(t => t.TenStatus == ConnectVeiculos.Core.Entities.Tenants.TenantStatus.Active)
                .Select(t => new { slug = t.TenSlug, nome = t.TenNome })
                .ToListAsync(ct);
            return Ok(tenants);
        }
    }
}
