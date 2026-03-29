using ConnectVeiculos.Application.InputModels.Usuarios;
using ConnectVeiculos.Application.Interfaces.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        #region GET

        [HttpGet]
        public async Task<IActionResult> ConsultarUsuarios(
            [FromServices] IConsultarUsuariosUseCase consultarUsuariosUseCase,
            [FromQuery] string pesquisa = "",
            [FromQuery] string inicio = "0",
            [FromQuery] string intervalo = "50")
        {
            var usuarios = await consultarUsuariosUseCase.Execute(pesquisa, inicio, intervalo);
            return Ok(usuarios);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> ConsultarUsuariosPaginado(
            [FromServices] IConsultarUsuariosPaginadoUseCase consultarUsuariosPaginadoUseCase,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var result = await consultarUsuariosPaginadoUseCase.Execute(page, pageSize, search);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ConsultarUsuarioPorId(
            [FromServices] IConsultarUsuarioPorIdUseCase consultarUsuarioPorIdUseCase,
            int id)
        {
            var usuario = await consultarUsuarioPorIdUseCase.Execute(id);

            if (usuario == null)
                return NotFound();

            return Ok(usuario);
        }

        #endregion

        #region POST

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> CadastrarUsuario(
            [FromServices] ICadastrarUsuarioUseCase cadastrarUsuarioUseCase,
            [FromBody] UsuarioInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados do usuario nao informados.");

            var id = await cadastrarUsuarioUseCase.Execute(inputModel);
            return CreatedAtAction(nameof(ConsultarUsuarioPorId), new { id }, new { id });
        }

        #endregion

        #region PUT

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> AtualizarUsuario(
            [FromServices] IAtualizarUsuarioUseCase atualizarUsuarioUseCase,
            int id,
            [FromBody] UsuarioInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados do usuario nao informados.");

            inputModel.UsuId = id;
            await atualizarUsuarioUseCase.Execute(inputModel);
            return NoContent();
        }

        #endregion

        #region DELETE

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> InativarUsuario(
            [FromServices] IInativarUsuarioUseCase inativarUsuarioUseCase,
            int id)
        {
            await inativarUsuarioUseCase.Execute(id);
            return NoContent();
        }

        #endregion
    }
}
