using ConnectVeiculos.Application.Interfaces.Relatorios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para geracao de relatorios
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador,Gerente")]
    [Produces("application/json")]
    public class RelatoriosController : ControllerBase
    {
        /// <summary>
        /// Gera relatorio de vendas
        /// </summary>
        /// <param name="consultarRelatorioVendasUseCase">Use case de relatorio injetado</param>
        /// <param name="dataInicio">Data inicial do periodo</param>
        /// <param name="dataFim">Data final do periodo</param>
        /// <param name="lojaId">Filtrar por loja especifica</param>
        /// <returns>Relatorio de vendas com totais e agrupamentos</returns>
        /// <response code="200">Relatorio gerado com sucesso</response>
        /// <response code="401">Usuario nao autenticado</response>
        [HttpGet("vendas")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RelatorioVendas(
            [FromServices] IConsultarRelatorioVendasUseCase consultarRelatorioVendasUseCase,
            [FromQuery] DateTime? dataInicio = null,
            [FromQuery] DateTime? dataFim = null,
            [FromQuery] int? lojaId = null)
        {
            var resultado = await consultarRelatorioVendasUseCase.Execute(dataInicio, dataFim, lojaId);
            return Ok(resultado);
        }

        /// <summary>
        /// Gera relatorio de estoque
        /// </summary>
        /// <param name="consultarRelatorioEstoqueUseCase">Use case de relatorio injetado</param>
        /// <param name="lojaId">Filtrar por loja especifica</param>
        /// <param name="categoriaId">Filtrar por categoria especifica</param>
        /// <returns>Relatorio de estoque com totais e agrupamentos</returns>
        /// <response code="200">Relatorio gerado com sucesso</response>
        /// <response code="401">Usuario nao autenticado</response>
        [HttpGet("estoque")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RelatorioEstoque(
            [FromServices] IConsultarRelatorioEstoqueUseCase consultarRelatorioEstoqueUseCase,
            [FromQuery] int? lojaId = null,
            [FromQuery] int? categoriaId = null)
        {
            var resultado = await consultarRelatorioEstoqueUseCase.Execute(lojaId, categoriaId);
            return Ok(resultado);
        }

        /// <summary>
        /// Gera relatorio financeiro
        /// </summary>
        /// <param name="consultarRelatorioFinanceiroUseCase">Use case de relatorio injetado</param>
        /// <param name="dataInicio">Data inicial do periodo</param>
        /// <param name="dataFim">Data final do periodo</param>
        /// <param name="lojaId">Filtrar por loja especifica</param>
        /// <returns>Relatorio financeiro com receitas, custos e lucros</returns>
        /// <response code="200">Relatorio gerado com sucesso</response>
        /// <response code="401">Usuario nao autenticado</response>
        [HttpGet("financeiro")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RelatorioFinanceiro(
            [FromServices] IConsultarRelatorioFinanceiroUseCase consultarRelatorioFinanceiroUseCase,
            [FromQuery] DateTime? dataInicio = null,
            [FromQuery] DateTime? dataFim = null,
            [FromQuery] int? lojaId = null)
        {
            var resultado = await consultarRelatorioFinanceiroUseCase.Execute(dataInicio, dataFim, lojaId);
            return Ok(resultado);
        }
    }
}
