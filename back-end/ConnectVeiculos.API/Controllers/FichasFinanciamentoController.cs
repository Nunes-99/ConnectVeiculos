using System.Security.Claims;
using ConnectVeiculos.Core.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Ficha unica de financiamento: bancos da loja com as tabelas de taxa
    /// (so Administrador/Gerente alteram), estimativa em todos os bancos e o
    /// retorno de cada proposta. Vendedor ve so as fichas que abriu.
    /// </summary>
    [ApiController]
    [Route("api/financiamento")]
    [Authorize]
    [EnableRateLimiting("api")]
    public class FichasFinanciamentoController : ControllerBase
    {
        private const string Gestao = "Administrador,Gerente";
        private readonly IFichaFinanciamentoService _fichas;

        public FichasFinanciamentoController(IFichaFinanciamentoService fichas)
        {
            _fichas = fichas;
        }

        [HttpGet("bancos-loja")]
        public async Task<IActionResult> Bancos() => Ok(await _fichas.ListarBancosAsync());

        [HttpPost("bancos-loja")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> SalvarBanco([FromBody] BancoLojaInfo input)
            => Executar(() => _fichas.SalvarBancoAsync(input));

        [HttpDelete("bancos-loja/{bancoId:int}")]
        [Authorize(Roles = Gestao)]
        public Task<IActionResult> RemoverBanco(int bancoId)
            => Executar(async () => { await _fichas.RemoverBancoAsync(bancoId); return new { ok = true }; });

        [HttpPost("estimativa")]
        public Task<IActionResult> Estimar([FromBody] EstimativaInput input)
            => Executar(() => _fichas.EstimarAsync(input));

        [HttpGet("fichas")]
        public async Task<IActionResult> Fichas() => Ok(await _fichas.ListarFichasAsync(SoDoVendedor()));

        [HttpGet("fichas/{fichaId:int}")]
        public Task<IActionResult> Ficha(int fichaId)
            => Executar(() => _fichas.ObterFichaAsync(fichaId, SoDoVendedor()));

        [HttpPost("fichas")]
        public Task<IActionResult> Criar([FromBody] FichaInput input)
            => Executar(() => _fichas.CriarFichaAsync(input, UsuarioId()));

        [HttpPut("fichas/{fichaId:int}")]
        public Task<IActionResult> Atualizar(int fichaId, [FromBody] FichaInput input)
            => Executar(() => _fichas.AtualizarFichaAsync(fichaId, input, SoDoVendedor()));

        public class StatusInput
        {
            public string Status { get; set; } = string.Empty;
        }

        [HttpPost("fichas/{fichaId:int}/status")]
        public Task<IActionResult> Status(int fichaId, [FromBody] StatusInput input)
            => Executar(() => _fichas.AlterarStatusAsync(fichaId, input.Status, SoDoVendedor()));

        [HttpPost("fichas/{fichaId:int}/propostas")]
        public Task<IActionResult> Proposta(int fichaId, [FromBody] PropostaBanco input)
            => Executar(() => _fichas.RegistrarPropostaAsync(fichaId, input, SoDoVendedor()));

        [HttpDelete("fichas/{fichaId:int}/propostas/{bancoId:int}")]
        public Task<IActionResult> RemoverProposta(int fichaId, int bancoId)
            => Executar(() => _fichas.RemoverPropostaAsync(fichaId, bancoId, SoDoVendedor()));

        private int UsuarioId()
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        /// <summary>Gestao ve todas as fichas; os demais, so as proprias.</summary>
        private int? SoDoVendedor()
            => User.IsInRole("Administrador") || User.IsInRole("Gerente") ? null : UsuarioId();

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (FinanciamentoException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }
}
