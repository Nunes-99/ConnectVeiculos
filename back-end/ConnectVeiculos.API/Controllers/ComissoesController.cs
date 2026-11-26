using System.Security.Claims;
using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.UseCases.Comissoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Comissoes dos vendedores: regras (padrao da loja e por vendedor), extrato
    /// do periodo e controle de pago/a pagar. O vendedor ve so as dele.
    /// </summary>
    [ApiController]
    [Route("api/comissoes")]
    [Authorize]
    public class ComissoesController : ControllerBase
    {
        private const string Gestao = "Administrador,Gerente";
        private readonly IComissoesUseCase _comissoes;

        public ComissoesController(IComissoesUseCase comissoes)
        {
            _comissoes = comissoes;
        }

        private bool EhGestao => User.IsInRole("Administrador") || User.IsInRole("Gerente") || User.IsInRole("Visualizador");

        private int? UsuarioId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value, out var id) ? id : null;

        [HttpGet("regras")]
        [Authorize(Roles = Gestao)]
        public async Task<IActionResult> Regras() => Ok(await _comissoes.RegrasAsync());

        [HttpPut("regras")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> SalvarRegras([FromBody] RegrasComissao regras) =>
            Executar(() => _comissoes.SalvarRegrasAsync(regras));

        /// <summary>Usada pela tela de venda ao escolher vendedor/valor.</summary>
        [HttpGet("sugestao")]
        public async Task<IActionResult> Sugestao([FromQuery] int vendedorId, [FromQuery] int veiculoId, [FromQuery] decimal valor)
        {
            var s = await _comissoes.SugestaoAsync(vendedorId, veiculoId, valor);
            return s == null ? NoContent() : Ok(s);
        }

        [HttpGet("extrato")]
        public async Task<IActionResult> Extrato([FromQuery] DateTime? inicio, [FromQuery] DateTime? fim, [FromQuery] int? vendedorId)
        {
            var hoje = DateTime.Today;
            var de = inicio ?? new DateTime(hoje.Year, hoje.Month, 1);
            var ate = fim ?? de.AddMonths(1).AddDays(-1);
            if (ate < de) return BadRequest(new { error = "A data final é antes da inicial." });
            // Vendedor ve so as proprias comissoes.
            if (!EhGestao) vendedorId = UsuarioId() ?? -1;
            return Ok(await _comissoes.ExtratoAsync(de, ate, vendedorId));
        }

        public class PagamentoInput
        {
            public List<int> VendaIds { get; set; } = new();
            /// <summary>Data do pagamento; nulo desfaz (volta para "a pagar").</summary>
            public DateTime? PagaEm { get; set; }
        }

        [HttpPost("pagar")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> Pagar([FromBody] PagamentoInput input) =>
            Executar(async () => new { atualizadas = await _comissoes.MarcarPagasAsync(input.VendaIds, input.PagaEm) });

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (InputModelException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
