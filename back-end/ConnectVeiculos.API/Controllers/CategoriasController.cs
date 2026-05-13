using ConnectVeiculos.Application.InputModels.Categorias;
using ConnectVeiculos.Application.Interfaces.Categorias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriasController : ControllerBase
    {
        #region GET

        [HttpGet]
        public async Task<IActionResult> ConsultarCategorias(
            [FromServices] IConsultarCategoriasUseCase consultarCategoriasUseCase,
            [FromQuery] string pesquisa = "",
            [FromQuery] string inicio = "0",
            [FromQuery] string intervalo = "50")
        {
            var categorias = await consultarCategoriasUseCase.Execute(pesquisa, inicio, intervalo);
            return Ok(categorias);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> ConsultarCategoriasPaginado(
            [FromServices] IConsultarCategoriasPaginadoUseCase consultarCategoriasPaginadoUseCase,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var result = await consultarCategoriasPaginadoUseCase.Execute(page, pageSize, search);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ConsultarCategoriaPorId(
            [FromServices] IConsultarCategoriaPorIdUseCase consultarCategoriaPorIdUseCase,
            int id)
        {
            var categoria = await consultarCategoriaPorIdUseCase.Execute(id);
            if (categoria == null)
                return NotFound("Categoria não encontrada.");
            return Ok(categoria);
        }

        #endregion

        #region POST

        [HttpPost]
        public async Task<IActionResult> CadastrarCategoria(
            [FromServices] ICadastrarCategoriaUseCase cadastrarCategoriaUseCase,
            [FromBody] CategoriaInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados da categoria não informados.");

            var id = await cadastrarCategoriaUseCase.Execute(inputModel);
            return CreatedAtAction(nameof(ConsultarCategoriaPorId), new { id }, new { id });
        }

        #endregion

        #region PUT

        [HttpPut("{id}")]
        public async Task<IActionResult> AtualizarCategoria(
            [FromServices] IAtualizarCategoriaUseCase atualizarCategoriaUseCase,
            int id,
            [FromBody] CategoriaInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados da categoria não informados.");

            inputModel.CatId = id;
            await atualizarCategoriaUseCase.Execute(inputModel);
            return NoContent();
        }

        #endregion

        #region DELETE

        [HttpDelete("{id}")]
        public async Task<IActionResult> InativarCategoria(
            [FromServices] IInativarCategoriaUseCase inativarCategoriaUseCase,
            int id)
        {
            await inativarCategoriaUseCase.Execute(id);
            return NoContent();
        }

        #endregion
    }
}
