using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para consulta da Tabela FIPE
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    [EnableRateLimiting("api")]
    public class FipeController : ControllerBase
    {
        private readonly IFipeService _fipeService;

        public FipeController(IFipeService fipeService)
        {
            _fipeService = fipeService;
        }

        /// <summary>
        /// Lista as marcas de veiculos por tipo
        /// </summary>
        /// <param name="tipo">1=Carros, 2=Motos, 3=Caminhoes</param>
        [HttpGet("marcas/{tipo}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMarcas(int tipo)
        {
            var tipoEnum = (FipeTipoVeiculo)tipo;
            var marcas = await _fipeService.GetMarcasAsync(tipoEnum);
            return Ok(marcas);
        }

        /// <summary>
        /// Lista os modelos de uma marca
        /// </summary>
        [HttpGet("modelos/{tipo}/{codigoMarca}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetModelos(int tipo, int codigoMarca)
        {
            var tipoEnum = (FipeTipoVeiculo)tipo;
            var modelos = await _fipeService.GetModelosAsync(tipoEnum, codigoMarca);
            return Ok(modelos);
        }

        /// <summary>
        /// Lista os anos disponiveis para um modelo
        /// </summary>
        [HttpGet("anos/{tipo}/{codigoMarca}/{codigoModelo}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAnos(int tipo, int codigoMarca, int codigoModelo)
        {
            var tipoEnum = (FipeTipoVeiculo)tipo;
            var anos = await _fipeService.GetAnosAsync(tipoEnum, codigoMarca, codigoModelo);
            return Ok(anos);
        }

        /// <summary>
        /// Consulta o preco FIPE de um veiculo
        /// </summary>
        [HttpGet("preco/{tipo}/{codigoMarca}/{codigoModelo}/{codigoAno}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPreco(int tipo, int codigoMarca, int codigoModelo, string codigoAno)
        {
            var tipoEnum = (FipeTipoVeiculo)tipo;
            var preco = await _fipeService.GetPrecoAsync(tipoEnum, codigoMarca, codigoModelo, codigoAno);

            if (preco == null)
                return NotFound("Preço não encontrado na tabela FIPE.");

            return Ok(preco);
        }
    }
}
