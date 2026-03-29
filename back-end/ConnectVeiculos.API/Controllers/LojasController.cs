using ConnectVeiculos.Application.InputModels.Lojas;
using ConnectVeiculos.Application.Interfaces.Lojas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LojasController : ControllerBase
    {
        #region GET

        [HttpGet]
        public async Task<IActionResult> ConsultarLojas(
            [FromServices] IConsultarLojasUseCase consultarLojasUseCase,
            [FromQuery] string pesquisa = "",
            [FromQuery] string inicio = "0",
            [FromQuery] string intervalo = "50")
        {
            var lojas = await consultarLojasUseCase.Execute(pesquisa, inicio, intervalo);
            return Ok(lojas);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> ConsultarLojasPaginado(
            [FromServices] IConsultarLojasPaginadoUseCase consultarLojasPaginadoUseCase,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var result = await consultarLojasPaginadoUseCase.Execute(page, pageSize, search);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ConsultarLojaPorId(
            [FromServices] IConsultarLojaPorIdUseCase consultarLojaPorIdUseCase,
            int id)
        {
            var loja = await consultarLojaPorIdUseCase.Execute(id);

            if (loja == null)
                return NotFound("Loja nao encontrada.");

            return Ok(loja);
        }

        #endregion

        #region POST

        [HttpPost]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> CadastrarLoja(
            [FromServices] ICadastrarLojaUseCase cadastrarLojaUseCase,
            [FromBody] LojaInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados da loja nao informados.");

            var id = await cadastrarLojaUseCase.Execute(inputModel);
            return CreatedAtAction(nameof(ConsultarLojaPorId), new { id }, new { id });
        }

        #endregion

        #region PUT

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> AtualizarLoja(
            [FromServices] IAtualizarLojaUseCase atualizarLojaUseCase,
            int id,
            [FromBody] LojaInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados da loja nao informados.");

            inputModel.LojId = id;
            await atualizarLojaUseCase.Execute(inputModel);
            return NoContent();
        }

        #endregion

        #region DELETE

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> InativarLoja(
            [FromServices] IInativarLojaUseCase inativarLojaUseCase,
            int id)
        {
            await inativarLojaUseCase.Execute(id);
            return NoContent();
        }

        #endregion
    }
}
