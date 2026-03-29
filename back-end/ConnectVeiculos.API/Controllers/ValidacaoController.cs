using ConnectVeiculos.Core.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para validacao de documentos brasileiros
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [EnableRateLimiting("public")]
    public class ValidacaoController : ControllerBase
    {
        /// <summary>
        /// Valida um CPF
        /// </summary>
        [HttpGet("cpf/{cpf}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ValidarCpf(string cpf)
        {
            var isValid = CpfValidator.IsValid(cpf);
            return Ok(new
            {
                cpf,
                valido = isValid,
                formatado = isValid ? CpfValidator.Format(cpf) : null
            });
        }

        /// <summary>
        /// Valida um CNPJ
        /// </summary>
        [HttpGet("cnpj/{cnpj}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ValidarCnpj(string cnpj)
        {
            var isValid = CnpjValidator.IsValid(cnpj);
            return Ok(new
            {
                cnpj,
                valido = isValid,
                formatado = isValid ? CnpjValidator.Format(cnpj) : null
            });
        }

        /// <summary>
        /// Valida uma placa de veiculo
        /// </summary>
        [HttpGet("placa/{placa}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ValidarPlaca(string placa)
        {
            var isValid = PlacaValidator.IsValid(placa);
            var tipo = PlacaValidator.GetTipo(placa);
            return Ok(new
            {
                placa,
                valido = isValid,
                tipo = tipo.ToString(),
                formatado = isValid ? PlacaValidator.Format(placa) : null
            });
        }

        /// <summary>
        /// Valida um chassi (VIN)
        /// </summary>
        [HttpGet("chassi/{chassi}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ValidarChassi(string chassi)
        {
            var isValid = ChassiValidator.IsValid(chassi);
            var info = isValid ? ChassiValidator.GetInfo(chassi) : null;
            return Ok(new
            {
                chassi,
                valido = isValid,
                info
            });
        }

        /// <summary>
        /// Valida multiplos documentos de uma vez
        /// </summary>
        [HttpPost("validar-multiplos")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ValidarMultiplos([FromBody] ValidacaoMultiplaRequest request)
        {
            var resultado = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(request.Cpf))
            {
                resultado["cpf"] = new
                {
                    valor = request.Cpf,
                    valido = CpfValidator.IsValid(request.Cpf),
                    formatado = CpfValidator.IsValid(request.Cpf) ? CpfValidator.Format(request.Cpf) : null
                };
            }

            if (!string.IsNullOrWhiteSpace(request.Cnpj))
            {
                resultado["cnpj"] = new
                {
                    valor = request.Cnpj,
                    valido = CnpjValidator.IsValid(request.Cnpj),
                    formatado = CnpjValidator.IsValid(request.Cnpj) ? CnpjValidator.Format(request.Cnpj) : null
                };
            }

            if (!string.IsNullOrWhiteSpace(request.Placa))
            {
                resultado["placa"] = new
                {
                    valor = request.Placa,
                    valido = PlacaValidator.IsValid(request.Placa),
                    tipo = PlacaValidator.GetTipo(request.Placa).ToString(),
                    formatado = PlacaValidator.IsValid(request.Placa) ? PlacaValidator.Format(request.Placa) : null
                };
            }

            if (!string.IsNullOrWhiteSpace(request.Chassi))
            {
                var isValid = ChassiValidator.IsValid(request.Chassi);
                resultado["chassi"] = new
                {
                    valor = request.Chassi,
                    valido = isValid,
                    info = isValid ? ChassiValidator.GetInfo(request.Chassi) : null
                };
            }

            return Ok(resultado);
        }
    }

    public class ValidacaoMultiplaRequest
    {
        public string Cpf { get; set; }
        public string Cnpj { get; set; }
        public string Placa { get; set; }
        public string Chassi { get; set; }
    }
}
