using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Certificado A1 (e-CNPJ) da loja, um so para Renave e NF-e. O arquivo e a
    /// senha nunca voltam: so titular, CNPJ e validade.
    /// </summary>
    [ApiController]
    [Route("api/certificado-digital")]
    [Authorize(Roles = "Administrador,Gerente")]
    [EnableRateLimiting("api")]
    public class CertificadoDigitalController : ControllerBase
    {
        private const long TamanhoMaximo = 1024 * 1024;
        private readonly ICertificadoDigitalService _certificado;

        public CertificadoDigitalController(ICertificadoDigitalService certificado)
        {
            _certificado = certificado;
        }

        [HttpGet]
        public async Task<IActionResult> Obter(CancellationToken ct) => Ok(await _certificado.ObterInfoAsync(ct));

        [HttpPost]
        [RequestSizeLimit(TamanhoMaximo)]
        public async Task<IActionResult> Enviar([FromForm] IFormFile arquivo, [FromForm] string senha, CancellationToken ct)
        {
            if (arquivo == null || arquivo.Length == 0) return BadRequest(new { error = "Envie o arquivo do certificado (.pfx ou .p12)." });
            using var ms = new MemoryStream();
            await arquivo.CopyToAsync(ms, ct);
            try
            {
                return Ok(await _certificado.SalvarAsync(ms.ToArray(), senha ?? "", ct));
            }
            catch (CertificadoDigitalException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Remover(CancellationToken ct) => Ok(await _certificado.RemoverAsync(ct));
    }
}
