using System.Text;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.NotasFiscais;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// NF-e de veiculos: configuracao do emitente e emissao/cancelamento por
    /// veiculo. Documento fiscal, por isso so Administrador e Gerente.
    /// </summary>
    [ApiController]
    [Route("api/nfe")]
    [Authorize(Roles = "Administrador,Gerente")]
    [EnableRateLimiting("api")]
    public class NfeController : ControllerBase
    {
        private readonly INfeService _nfe;
        private readonly ICertificadoDigitalService _certificado;

        public NfeController(INfeService nfe, ICertificadoDigitalService certificado)
        {
            _nfe = nfe;
            _certificado = certificado;
        }

        [HttpGet("config")]
        public async Task<IActionResult> ObterConfig(CancellationToken ct) => Ok(await _nfe.ObterConfigAsync(ct));

        [HttpPut("config")]
        public Task<IActionResult> SalvarConfig([FromBody] NfeConfigInfo input, CancellationToken ct)
            => Executar(() => _nfe.SalvarConfigAsync(input, ct));

        /// <summary>O mesmo certificado A1 do Renave; aqui so para conferir.</summary>
        [HttpGet("certificado")]
        public async Task<IActionResult> Certificado(CancellationToken ct) => Ok(await _certificado.ObterInfoAsync(ct));

        [HttpPost("status-sefaz")]
        public Task<IActionResult> StatusSefaz(CancellationToken ct) => Executar(() => _nfe.ConsultarStatusSefazAsync(ct));

        [HttpGet("municipios")]
        public Task<IActionResult> Municipios([FromQuery] string uf, [FromQuery] string? nome, CancellationToken ct)
            => Executar(() => _nfe.BuscarMunicipiosAsync(uf, nome, ct));

        [HttpGet("veiculos/{veiculoId:int}")]
        public async Task<IActionResult> DoVeiculo(int veiculoId, CancellationToken ct)
            => Ok(await _nfe.ListarDoVeiculoAsync(veiculoId, ct));

        [HttpGet("veiculos/{veiculoId:int}/sugestao")]
        public Task<IActionResult> Sugestao(int veiculoId, [FromQuery] string tipo, CancellationToken ct)
            => Executar(() => _nfe.SugerirAsync(veiculoId, tipo, ct));

        [HttpPost("veiculos/{veiculoId:int}")]
        public Task<IActionResult> Emitir(int veiculoId, [FromBody] NfeEmissaoInput input, CancellationToken ct)
            => Executar(() => _nfe.EmitirAsync(veiculoId, input, ct));

        [HttpPost("{notaId:int}/reenviar")]
        public Task<IActionResult> Reenviar(int notaId, [FromBody] NfeEmissaoInput? correcao, CancellationToken ct)
            => Executar(() => _nfe.ReenviarAsync(notaId, correcao, ct));

        public class CancelamentoInput
        {
            public string Justificativa { get; set; } = string.Empty;
        }

        [HttpPost("{notaId:int}/cancelar")]
        public Task<IActionResult> Cancelar(int notaId, [FromBody] CancelamentoInput input, CancellationToken ct)
            => Executar(() => _nfe.CancelarAsync(notaId, input.Justificativa, ct));

        [HttpGet("{notaId:int}/xml")]
        public async Task<IActionResult> Xml(int notaId, CancellationToken ct)
        {
            try
            {
                var (xml, nome) = await _nfe.ObterXmlAsync(notaId, ct);
                return File(Encoding.UTF8.GetBytes(xml), "application/xml", nome);
            }
            catch (NfeException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        [HttpGet("{notaId:int}/danfe")]
        public async Task<IActionResult> Danfe(int notaId, CancellationToken ct)
        {
            try
            {
                var (pdf, nome) = await _nfe.ObterDanfeAsync(notaId, ct);
                return File(pdf, "application/pdf", nome);
            }
            catch (NfeException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (NfeException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }
}
