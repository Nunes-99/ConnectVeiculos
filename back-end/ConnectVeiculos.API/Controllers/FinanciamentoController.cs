using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para simulacao de financiamento
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [EnableRateLimiting("public")]
    public class FinanciamentoController : ControllerBase
    {
        private readonly IFinanciamentoService _financiamentoService;

        public FinanciamentoController(IFinanciamentoService financiamentoService)
        {
            _financiamentoService = financiamentoService;
        }

        /// <summary>
        /// Simula financiamento pela Tabela Price (parcelas fixas)
        /// </summary>
        [HttpPost("simular/price")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult SimularPrice([FromBody] SimulacaoRequest request)
        {
            if (!ValidarRequest(request, out string erro))
                return BadRequest(erro);

            var simulacao = _financiamentoService.CalcularPrice(
                request.ValorVeiculo,
                request.Entrada,
                request.NumeroParcelas,
                request.TaxaJurosAnual);

            return Ok(simulacao);
        }

        /// <summary>
        /// Simula financiamento pelo Sistema SAC (amortizacao constante)
        /// </summary>
        [HttpPost("simular/sac")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult SimularSAC([FromBody] SimulacaoRequest request)
        {
            if (!ValidarRequest(request, out string erro))
                return BadRequest(erro);

            var simulacao = _financiamentoService.CalcularSAC(
                request.ValorVeiculo,
                request.Entrada,
                request.NumeroParcelas,
                request.TaxaJurosAnual);

            return Ok(simulacao);
        }

        /// <summary>
        /// Compara financiamento Price vs SAC
        /// </summary>
        [HttpPost("comparar")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Comparar([FromBody] SimulacaoRequest request)
        {
            if (!ValidarRequest(request, out string erro))
                return BadRequest(erro);

            var price = _financiamentoService.CalcularPrice(
                request.ValorVeiculo,
                request.Entrada,
                request.NumeroParcelas,
                request.TaxaJurosAnual);

            var sac = _financiamentoService.CalcularSAC(
                request.ValorVeiculo,
                request.Entrada,
                request.NumeroParcelas,
                request.TaxaJurosAnual);

            return Ok(new
            {
                price,
                sac,
                diferenca = new
                {
                    totalJuros = price.TotalJuros - sac.TotalJuros,
                    totalPago = price.ValorTotalPago - sac.ValorTotalPago
                }
            });
        }

        private bool ValidarRequest(SimulacaoRequest request, out string erro)
        {
            erro = null;

            if (request == null)
            {
                erro = "Dados de simulação são obrigatórios.";
                return false;
            }

            if (request.ValorVeiculo <= 0)
            {
                erro = "O valor do veículo deve ser maior que zero.";
                return false;
            }

            if (request.Entrada < 0 || request.Entrada >= request.ValorVeiculo)
            {
                erro = "A entrada deve ser maior ou igual a zero e menor que o valor do veículo.";
                return false;
            }

            if (request.NumeroParcelas < 1 || request.NumeroParcelas > 84)
            {
                erro = "O numero de parcelas deve estar entre 1 e 84.";
                return false;
            }

            if (request.TaxaJurosAnual <= 0 || request.TaxaJurosAnual > 100)
            {
                erro = "A taxa de juros anual deve estar entre 0 e 100%.";
                return false;
            }

            return true;
        }
    }

    public class SimulacaoRequest
    {
        public decimal ValorVeiculo { get; set; }
        public decimal Entrada { get; set; }
        public int NumeroParcelas { get; set; }
        public decimal TaxaJurosAnual { get; set; }
    }
}
