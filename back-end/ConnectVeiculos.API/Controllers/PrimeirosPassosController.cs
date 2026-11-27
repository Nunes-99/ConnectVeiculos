using ConnectVeiculos.Infrastructure.Services.PrimeirosPassos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>"Primeiros passos" da loja no Painel (gestao).</summary>
    [ApiController]
    [Route("api/primeiros-passos")]
    [Authorize(Roles = "Administrador,Gerente")]
    public class PrimeirosPassosController : ControllerBase
    {
        private readonly PrimeirosPassosService _passos;

        public PrimeirosPassosController(PrimeirosPassosService passos)
        {
            _passos = passos;
        }

        [HttpGet]
        public async Task<IActionResult> Consultar() => Ok(await _passos.ConsultarAsync());

        public class OcultarInput
        {
            public bool Oculto { get; set; }
        }

        [HttpPut("oculto")]
        public async Task<IActionResult> Ocultar([FromBody] OcultarInput input) => Ok(await _passos.OcultarAsync(input.Oculto));
    }
}
