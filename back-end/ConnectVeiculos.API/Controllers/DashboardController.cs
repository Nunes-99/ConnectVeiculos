using ConnectVeiculos.Application.Interfaces.Dashboard;
using ConnectVeiculos.Application.ViewModels.Dashboard;
using ConnectVeiculos.Infrastructure.Cache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para dados do dashboard
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class DashboardController : ControllerBase
    {
        private readonly ICacheService _cacheService;

        public DashboardController(ICacheService cacheService)
        {
            _cacheService = cacheService;
        }

        /// <summary>
        /// Retorna os dados do dashboard com estatisticas gerais
        /// </summary>
        /// <param name="consultarDashboardUseCase">Use case injetado</param>
        /// <returns>Dados do dashboard</returns>
        /// <response code="200">Dashboard retornado com sucesso</response>
        /// <response code="401">Usuario nao autenticado</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarDashboard(
            [FromServices] IConsultarDashboardUseCase consultarDashboardUseCase)
        {
            var cached = _cacheService.Get<DashboardViewModel>(CacheKeys.Dashboard);
            if (cached != null)
            {
                return Ok(cached);
            }

            var dashboard = await consultarDashboardUseCase.Execute();
            _cacheService.Set(CacheKeys.Dashboard, dashboard, TimeSpan.FromMinutes(1));

            return Ok(dashboard);
        }

        /// <summary>
        /// Retorna vendas por periodo para grafico de linha
        /// </summary>
        /// <param name="dataInicio">Data de inicio do periodo</param>
        /// <param name="dataFim">Data de fim do periodo</param>
        /// <param name="useCase">Use case injetado</param>
        /// <returns>Vendas agrupadas por dia</returns>
        [HttpGet("vendas-periodo")]
        [ProducesResponseType(typeof(VendasPorPeriodoViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarVendasPorPeriodo(
            [FromQuery] DateTime? dataInicio,
            [FromQuery] DateTime? dataFim,
            [FromServices] IConsultarVendasPorPeriodoUseCase useCase)
        {
            var inicio = dataInicio ?? DateTime.Today.AddDays(-30);
            var fim = dataFim ?? DateTime.Today;

            var resultado = await useCase.Execute(inicio, fim);
            return Ok(resultado);
        }

        /// <summary>
        /// Retorna faturamento mensal para grafico de barras
        /// </summary>
        /// <param name="ano">Ano de referencia (padrao: ano atual)</param>
        /// <param name="useCase">Use case injetado</param>
        /// <returns>Faturamento por mes</returns>
        [HttpGet("faturamento-mensal")]
        [ProducesResponseType(typeof(FaturamentoMensalViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarFaturamentoMensal(
            [FromQuery] int? ano,
            [FromServices] IConsultarFaturamentoMensalUseCase useCase)
        {
            var anoConsulta = ano ?? DateTime.Today.Year;
            var resultado = await useCase.Execute(anoConsulta);
            return Ok(resultado);
        }

        /// <summary>
        /// Retorna os veiculos mais vendidos
        /// </summary>
        /// <param name="quantidade">Quantidade de veiculos a retornar (padrao: 10)</param>
        /// <param name="useCase">Use case injetado</param>
        /// <returns>Top veiculos vendidos</returns>
        [HttpGet("top-veiculos")]
        [ProducesResponseType(typeof(TopVeiculosVendidosViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarTopVeiculos(
            [FromQuery] int? quantidade,
            [FromServices] IConsultarTopVeiculosUseCase useCase)
        {
            var qtd = quantidade ?? 10;
            var resultado = await useCase.Execute(qtd);
            return Ok(resultado);
        }

        /// <summary>
        /// Retorna comparativo entre mes atual e anterior
        /// </summary>
        /// <param name="useCase">Use case injetado</param>
        /// <returns>Comparativo mensal</returns>
        [HttpGet("comparativo-mensal")]
        [ProducesResponseType(typeof(ComparativoMensalViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarComparativoMensal(
            [FromServices] IConsultarComparativoMensalUseCase useCase)
        {
            var resultado = await useCase.Execute();
            return Ok(resultado);
        }
    }
}
