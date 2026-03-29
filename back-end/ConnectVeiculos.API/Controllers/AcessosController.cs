using ConnectVeiculos.Application.InputModels.Acessos;
using ConnectVeiculos.Application.Interfaces.Acessos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")]
    public class AcessosController : ControllerBase
    {
        #region GET

        [HttpGet]
        public async Task<IActionResult> ConsultarAcessos(
            [FromServices] IConsultarAcessosUseCase consultarAcessosUseCase,
            [FromQuery] string pesquisa = "",
            [FromQuery] string inicio = "0",
            [FromQuery] string intervalo = "50")
        {
            var acessos = await consultarAcessosUseCase.Execute(pesquisa, inicio, intervalo);
            return Ok(acessos);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> ConsultarAcessosPaginado(
            [FromServices] IConsultarAcessosPaginadoUseCase consultarAcessosPaginadoUseCase,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var result = await consultarAcessosPaginadoUseCase.Execute(page, pageSize, search);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ConsultarAcessoPorId(
            [FromServices] IConsultarAcessoPorIdUseCase consultarAcessoPorIdUseCase,
            int id)
        {
            var acesso = await consultarAcessoPorIdUseCase.Execute(id);

            if (acesso == null)
                return NotFound("Acesso nao encontrado.");

            return Ok(acesso);
        }

        #endregion

        #region POST

        [HttpPost]
        public async Task<IActionResult> CadastrarAcesso(
            [FromServices] ICadastrarAcessoUseCase cadastrarAcessoUseCase,
            [FromBody] AcessoInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados do acesso nao informados.");

            var id = await cadastrarAcessoUseCase.Execute(inputModel);
            return CreatedAtAction(nameof(ConsultarAcessoPorId), new { id }, new { id });
        }

        #endregion

        #region PUT

        [HttpPut("{id}")]
        public async Task<IActionResult> AtualizarAcesso(
            [FromServices] IAtualizarAcessoUseCase atualizarAcessoUseCase,
            int id,
            [FromBody] AcessoInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados do acesso nao informados.");

            inputModel.AcsId = id;
            await atualizarAcessoUseCase.Execute(inputModel);
            return NoContent();
        }

        #endregion

        #region DELETE

        [HttpDelete("{id}")]
        public async Task<IActionResult> InativarAcesso(
            [FromServices] IInativarAcessoUseCase inativarAcessoUseCase,
            int id)
        {
            await inativarAcessoUseCase.Execute(id);
            return NoContent();
        }

        #endregion
    }
}
