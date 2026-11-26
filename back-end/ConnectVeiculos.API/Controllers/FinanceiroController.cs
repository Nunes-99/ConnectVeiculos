using System.Security.Claims;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Financeiro simples: contas a pagar e a receber e o fluxo de caixa do mes.
    /// Mostra o caixa da loja: so gestao (Visualizador so consulta).
    /// </summary>
    [ApiController]
    [Route("api/financeiro")]
    [Authorize(Roles = "Administrador,Gerente,Visualizador")]
    public class FinanceiroController : ControllerBase
    {
        private const string Gestao = "Administrador,Gerente";
        private readonly IFinanceiroService _financeiro;

        public FinanceiroController(IFinanceiroService financeiro)
        {
            _financeiro = financeiro;
        }

        [HttpGet]
        public Task<IActionResult> Mes([FromQuery] int? ano, [FromQuery] int? mes, [FromQuery] int? lojaId) =>
            Executar(() => _financeiro.MesAsync(ano ?? DateTime.Today.Year, mes ?? DateTime.Today.Month, lojaId));

        [HttpPost("lancamentos")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Criar([FromBody] LancamentoInput input)
        {
            var usuarioId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value, out var id) ? id : 0;
            return Executar(() => _financeiro.CriarAsync(input, usuarioId));
        }

        [HttpPut("lancamentos/{id:int}")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Alterar(int id, [FromBody] LancamentoInput input) =>
            Executar(() => _financeiro.AlterarAsync(id, input));

        [HttpDelete("lancamentos/{id:int}")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Excluir(int id) =>
            Executar(async () => { await _financeiro.ExcluirAsync(id); return new { ok = true }; });

        public class PagamentoInput
        {
            /// <summary>Data do pagamento/recebimento; nulo desfaz.</summary>
            public DateTime? PagoEm { get; set; }
        }

        [HttpPost("lancamentos/{id:int}/pagar")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Pagar(int id, [FromBody] PagamentoInput input) =>
            Executar(() => _financeiro.PagarAsync(id, input.PagoEm));

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (FinanceiroException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
