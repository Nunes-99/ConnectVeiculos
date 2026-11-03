using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Webmotors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Webmotors: a gerencia conecta o usuario de integracao da loja e anuncia
    /// cada veiculo (escolhendo uma vez marca/modelo/versao nas tabelas dela).
    /// Depois disso, editar, vender e estornar atualizam o anuncio sozinhos.
    /// </summary>
    [ApiController]
    [Route("api/integracoes/webmotors")]
    [Authorize(Roles = Gestao)]
    [EnableRateLimiting("api")]
    public class WebmotorsController : ControllerBase
    {
        private const string Gestao = "Administrador,Gerente";
        private readonly IWebmotorsService _webmotors;

        public WebmotorsController(IWebmotorsService webmotors)
        {
            _webmotors = webmotors;
        }

        [HttpGet]
        public Task<IActionResult> Status() => Executar(() => _webmotors.StatusAsync());

        [HttpPost("conectar")]
        public Task<IActionResult> Conectar([FromBody] WebmotorsConectarInput input) => Executar(() => _webmotors.ConectarAsync(input));

        public class ModalidadeInput
        {
            public long ModalidadeId { get; set; }
        }

        [HttpPut("modalidade")]
        public Task<IActionResult> Modalidade([FromBody] ModalidadeInput input) => Executar(() => _webmotors.EscolherModalidadeAsync(input.ModalidadeId));

        [HttpDelete]
        public Task<IActionResult> Desconectar() => Executar(async () => { await _webmotors.DesconectarAsync(); return new { ok = true }; });

        [HttpGet("tabelas/{tabela}")]
        public Task<IActionResult> Tabela(string tabela) => Executar(() => _webmotors.TabelaAsync(tabela));

        [HttpGet("marcas/{marcaId:long}/modelos")]
        public Task<IActionResult> Modelos(long marcaId) => Executar(() => _webmotors.ModelosAsync(marcaId));

        [HttpGet("modelos/{modeloId:long}/versoes")]
        public Task<IActionResult> Versoes(long modeloId, [FromQuery] int? ano) => Executar(() => _webmotors.VersoesAsync(modeloId, ano));

        [HttpGet("veiculos/{veiculoId:int}")]
        public Task<IActionResult> Veiculo(int veiculoId) => Executar(() => _webmotors.ObterVeiculoAsync(veiculoId));

        [HttpPost("veiculos/{veiculoId:int}")]
        public Task<IActionResult> Publicar(int veiculoId, [FromBody] WebmotorsAnuncioDados dados)
            => Executar(() => _webmotors.PublicarAsync(veiculoId, dados));

        [HttpDelete("veiculos/{veiculoId:int}")]
        public Task<IActionResult> Remover(int veiculoId) => Executar(() => _webmotors.RemoverAsync(veiculoId));

        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (WebmotorsException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }
}
