using ConnectVeiculos.Application.InputModels.Vendas;
using ConnectVeiculos.Application.Interfaces.Vendas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para gerenciamento de vendas
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class VendasController : ControllerBase
    {
        #region GET

        /// <summary>
        /// Lista todas as vendas
        /// </summary>
        /// <param name="consultarVendasUseCase">Use case de consulta injetado</param>
        /// <returns>Lista de vendas</returns>
        /// <response code="200">Lista de vendas retornada com sucesso</response>
        /// <response code="401">Usuario nao autenticado</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarVendas(
            [FromServices] IConsultarVendasUseCase consultarVendasUseCase)
        {
            var vendas = await consultarVendasUseCase.Execute();
            return Ok(vendas);
        }

        /// <summary>
        /// Consulta uma venda pelo ID
        /// </summary>
        /// <param name="consultarVendaPorIdUseCase">Use case de consulta injetado</param>
        /// <param name="id">ID da venda</param>
        /// <returns>Dados da venda</returns>
        /// <response code="200">Venda encontrada</response>
        /// <response code="401">Usuario nao autenticado</response>
        /// <response code="404">Venda nao encontrada</response>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConsultarVendaPorId(
            [FromServices] IConsultarVendaPorIdUseCase consultarVendaPorIdUseCase,
            int id)
        {
            var venda = await consultarVendaPorIdUseCase.Execute(id);

            if (venda == null)
                return NotFound();

            return Ok(venda);
        }

        #endregion

        #region POST

        /// <summary>
        /// Registra uma nova venda
        /// </summary>
        /// <param name="registrarVendaUseCase">Use case de registro injetado</param>
        /// <param name="inputModel">Dados da venda</param>
        /// <returns>ID da venda criada</returns>
        /// <response code="201">Venda registrada com sucesso</response>
        /// <response code="400">Dados invalidos ou veiculo indisponivel</response>
        /// <response code="401">Usuario nao autenticado</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RegistrarVenda(
            [FromServices] IRegistrarVendaUseCase registrarVendaUseCase,
            [FromBody] VendaInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados da venda não informados.");

            var id = await registrarVendaUseCase.Execute(inputModel);
            return CreatedAtAction(nameof(ConsultarVendaPorId), new { id }, new { id });
        }

        /// <summary>
        /// Atualiza dados nao-financeiros de uma venda (comprador, forma de pagamento, observacao).
        /// Valor, comissao, veiculo e data sao imutaveis.
        /// </summary>
        /// <response code="204">Venda atualizada com sucesso</response>
        /// <response code="400">Dados invalidos ou venda estornada</response>
        /// <response code="404">Venda nao encontrada</response>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AtualizarVenda(
            [FromServices] IAtualizarVendaUseCase atualizarVendaUseCase,
            int id,
            [FromBody] VendaInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados da venda não informados.");

            await atualizarVendaUseCase.Execute(id, inputModel);
            return NoContent();
        }

        /// <summary>
        /// Estorna uma venda existente
        /// </summary>
        /// <param name="estornarVendaUseCase">Use case de estorno injetado</param>
        /// <param name="id">ID da venda a ser estornada</param>
        /// <returns>Mensagem de confirmacao</returns>
        /// <response code="200">Venda estornada com sucesso</response>
        /// <response code="400">Venda ja estornada</response>
        /// <response code="401">Usuario nao autenticado</response>
        /// <response code="404">Venda nao encontrada</response>
        [HttpPost("{id}/estornar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EstornarVenda(
            [FromServices] IEstornarVendaUseCase estornarVendaUseCase,
            int id)
        {
            await estornarVendaUseCase.Execute(id);
            return Ok(new { message = "Venda estornada com sucesso." });
        }

        #endregion
    }
}
