using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.Interfaces.Estoque;
using ConnectVeiculos.Application.UseCases.Estoque;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Carros parados no estoque e sugestao de preco. Mostra custo e margem,
    /// por isso segue a regra do Dashboard: vendedor nao ve.
    /// </summary>
    [ApiController]
    [Route("api/estoque-parado")]
    [Authorize(Roles = "Administrador,Gerente,Visualizador")]
    public class EstoqueParadoController : ControllerBase
    {
        private readonly IEstoqueParadoUseCase _estoque;

        public EstoqueParadoController(IEstoqueParadoUseCase estoque)
        {
            _estoque = estoque;
        }

        [HttpGet]
        public async Task<IActionResult> Consultar([FromQuery] int? lojaId) => Ok(await _estoque.ConsultarAsync(lojaId));

        [HttpGet("veiculos/{veiculoId:int}")]
        public async Task<IActionResult> Sugestao(int veiculoId)
        {
            var item = await _estoque.SugestaoAsync(veiculoId);
            return item == null ? NotFound(new { error = "Veículo não encontrado." }) : Ok(item);
        }

        [HttpGet("config")]
        public async Task<IActionResult> Config() => Ok(await _estoque.ConfigAsync());

        [HttpPut("config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> SalvarConfig([FromBody] EstoqueParadoConfig config)
        {
            try
            {
                return Ok(await _estoque.SalvarConfigAsync(config));
            }
            catch (InputModelException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
