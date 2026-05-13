using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para geracao de QR Codes
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [EnableRateLimiting("public")]
    public class QrCodeController : ControllerBase
    {
        private readonly IQrCodeService _qrCodeService;

        public QrCodeController(IQrCodeService qrCodeService)
        {
            _qrCodeService = qrCodeService;
        }

        /// <summary>
        /// Gera QR Code para um veiculo (retorna imagem PNG)
        /// </summary>
        [HttpGet("veiculo/{veiculoId}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetQrCodeVeiculo(int veiculoId, [FromQuery] string baseUrl = null)
        {
            var url = baseUrl ?? $"{Request.Scheme}://{Request.Host}";
            var qrCode = _qrCodeService.GerarQrCodeVeiculo(veiculoId, url);
            return File(qrCode, "image/png");
        }

        /// <summary>
        /// Gera QR Code para um veiculo (retorna base64)
        /// </summary>
        [HttpGet("veiculo/{veiculoId}/base64")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetQrCodeVeiculoBase64(int veiculoId, [FromQuery] string baseUrl = null)
        {
            var url = baseUrl ?? $"{Request.Scheme}://{Request.Host}";
            var qrCode = _qrCodeService.GerarQrCodeVeiculo(veiculoId, url);
            var base64 = Convert.ToBase64String(qrCode);
            return Ok(new { qrCode = $"data:image/png;base64,{base64}" });
        }

        /// <summary>
        /// Gera QR Code para um conteudo customizado
        /// </summary>
        [HttpPost("gerar")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GerarQrCode([FromBody] QrCodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Conteudo))
                return BadRequest("O conteúdo é obrigatório.");

            var tamanho = request.Tamanho > 0 ? request.Tamanho : 250;
            var qrCode = _qrCodeService.GerarQrCode(request.Conteudo, tamanho);

            if (request.RetornarBase64)
            {
                var base64 = Convert.ToBase64String(qrCode);
                return Ok(new { qrCode = $"data:image/png;base64,{base64}" });
            }

            return File(qrCode, "image/png");
        }
    }

    public class QrCodeRequest
    {
        public string Conteudo { get; set; }
        public int Tamanho { get; set; } = 250;
        public bool RetornarBase64 { get; set; } = true;
    }
}
