using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/financiamento/bancos")]
    [Authorize]
    public class FinanciamentoBancosController : ControllerBase
    {
        private readonly IEnumerable<IBancoFinanciamentoService> _bancos;

        public FinanciamentoBancosController(IEnumerable<IBancoFinanciamentoService> bancos)
        {
            _bancos = bancos;
        }

        [HttpGet]
        public IActionResult ListarBancos()
        {
            var bancos = _bancos.Select(b => new
            {
                b.CodigoBanco,
                b.NomeBanco,
                configurado = b.IsConfigured()
            });
            return Ok(bancos);
        }

        [HttpPost("simular/{codigoBanco}")]
        public async Task<IActionResult> SimularBanco(string codigoBanco, [FromBody] BancoSimulacaoRequest request)
        {
            var banco = _bancos.FirstOrDefault(b => b.CodigoBanco.Equals(codigoBanco, StringComparison.OrdinalIgnoreCase) && b.IsConfigured());
            if (banco == null)
                return NotFound($"Banco '{codigoBanco}' não encontrado ou não configurado.");

            var resultado = await banco.SimularAsync(request);
            return Ok(resultado);
        }

        [HttpPost("simular-todos")]
        public async Task<IActionResult> SimularTodos([FromBody] BancoSimulacaoRequest request)
        {
            var bancosConfigurados = _bancos.Where(b => b.IsConfigured()).ToList();
            if (!bancosConfigurados.Any())
                return BadRequest("Nenhum banco configurado para financiamento.");

            var tarefas = bancosConfigurados.Select(b => b.SimularAsync(request));
            var resultados = await Task.WhenAll(tarefas);

            return Ok(resultados.OrderByDescending(r => r.Aprovado).ThenBy(r => r.TaxaMensal));
        }

        [HttpPost("proposta/{codigoBanco}")]
        public async Task<IActionResult> EnviarProposta(string codigoBanco, [FromBody] BancoPropostaRequest request)
        {
            var banco = _bancos.FirstOrDefault(b => b.CodigoBanco.Equals(codigoBanco, StringComparison.OrdinalIgnoreCase) && b.IsConfigured());
            if (banco == null)
                return NotFound($"Banco '{codigoBanco}' não encontrado ou não configurado.");

            var resultado = await banco.EnviarPropostaAsync(request);
            return Ok(resultado);
        }

        [HttpGet("proposta/{codigoBanco}/{propostaExternaId}")]
        public async Task<IActionResult> ConsultarStatus(string codigoBanco, string propostaExternaId)
        {
            var banco = _bancos.FirstOrDefault(b => b.CodigoBanco.Equals(codigoBanco, StringComparison.OrdinalIgnoreCase));
            if (banco == null)
                return NotFound($"Banco '{codigoBanco}' não encontrado.");

            var status = await banco.ConsultarStatusAsync(propostaExternaId);
            return Ok(status);
        }
    }
}
