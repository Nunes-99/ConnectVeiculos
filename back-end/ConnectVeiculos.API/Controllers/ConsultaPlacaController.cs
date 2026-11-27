using System.Security.Claims;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Services.ConsultaPlaca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Consultas da ZapCar (veiculo, documentos e pessoa) pagas com creditos da
    /// loja. Quem avalia carro (gestao e vendedor) consulta; so a gestao compra creditos.
    /// </summary>
    [ApiController]
    [Route("api/consulta-placa")]
    [Authorize(Roles = "Administrador,Gerente,Vendedor,Visualizador")]
    public class ConsultaPlacaController : ControllerBase
    {
        private const string Consultam = "Administrador,Gerente,Vendedor";
        private const string Gestao = "Administrador,Gerente";

        private readonly ConsultaPlacaService _consultas;
        private readonly ITenantContext _tenant;

        public ConsultaPlacaController(ConsultaPlacaService consultas, ITenantContext tenant)
        {
            _consultas = consultas;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<IActionResult> Painel(CancellationToken ct)
        {
            if (!_tenant.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            return Ok(await _consultas.PainelAsync(_tenant.TenantId, ct));
        }

        [HttpPost]
        [Authorize(Roles = Consultam)]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Consultar([FromBody] PedidoConsulta pedido, CancellationToken ct)
        {
            if (!_tenant.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            var usuarioId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value, out var id) ? id : 0;
            try
            {
                return Ok(await _consultas.ConsultarAsync(_tenant.TenantId, pedido, usuarioId, ct));
            }
            catch (DomainException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obter(int id, CancellationToken ct)
        {
            if (!_tenant.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            var item = await _consultas.ObterAsync(_tenant.TenantId, id, ct);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpGet("{id:int}/documento")]
        public async Task<IActionResult> Documento(int id, CancellationToken ct)
        {
            var doc = await _consultas.DocumentoAsync(id, ct);
            if (doc == null) return NotFound(new { error = "O documento desta consulta não está disponível." });
            var extensao = doc.Value.Tipo.Contains("pdf") ? "pdf" : doc.Value.Tipo.Contains("png") ? "png" : "jpg";
            return File(doc.Value.Conteudo, doc.Value.Tipo, $"consulta-{id}.{extensao}");
        }

        /// <summary>Aviso que precisa ser lido e aceito antes da 2a via do ATPV-e.</summary>
        [HttpGet("aviso-atpve")]
        public async Task<IActionResult> AvisoAtpve(CancellationToken ct)
        {
            var aviso = await _consultas.AvisoAtpveAsync(ct);
            return aviso == null ? StatusCode(503, new { error = "Não consegui carregar o aviso do ATPV-e. Tente de novo." }) : Ok(aviso);
        }

        public class CreditosInput
        {
            public decimal Valor { get; set; }
            public string Email { get; set; } = "";
        }

        [HttpPost("creditos/pix")]
        [Authorize(Roles = Gestao)]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> ComprarCreditos(
            [FromBody] CreditosInput input,
            [FromServices] IAssinaturaService pagamentos,
            [FromServices] IOptions<ZapCarSettings> settings,
            CancellationToken ct)
        {
            if (!_tenant.IsResolved) return BadRequest(new { error = "tenant_nao_resolvido" });
            if (!settings.Value.ValoresPacotes().Contains(input.Valor))
                return UnprocessableEntity(new { error = "Escolha um dos valores de créditos." });
            try
            {
                return Ok(await pagamentos.GerarPixCreditosAsync(_tenant.TenantId, input.Valor, input.Email, ct));
            }
            catch (AssinaturaException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Precos das consultas por plano (tela da plataforma). Fora do login das
    /// lojas: autentica pelo header X-Admin-Token = ADMIN_API_TOKEN do servidor.
    /// </summary>
    [ApiController]
    [Route("api/admin/consultas/precos")]
    [AllowAnonymous]
    public class PrecosConsultaAdminController : ControllerBase
    {
        private readonly PrecosConsultaService _precos;
        private readonly IZapCarClient _zapcar;
        private readonly IOptions<ZapCarSettings> _settings;
        private readonly ILogger<PrecosConsultaAdminController> _logger;
        private readonly string? _adminToken;

        public PrecosConsultaAdminController(PrecosConsultaService precos, IZapCarClient zapcar, IOptions<ZapCarSettings> settings,
            IConfiguration configuration, ILogger<PrecosConsultaAdminController> logger)
        {
            _precos = precos;
            _zapcar = zapcar;
            _settings = settings;
            _logger = logger;
            _adminToken = Environment.GetEnvironmentVariable("ADMIN_API_TOKEN") ?? configuration["AdminApi:Token"];
        }

        private IActionResult? Negado()
        {
            if (string.IsNullOrEmpty(_adminToken))
                return StatusCode(503, new { error = "ADMIN_API_TOKEN não configurado no servidor." });
            var enviado = Request.Headers["X-Admin-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(enviado)
                || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(enviado), System.Text.Encoding.UTF8.GetBytes(_adminToken)))
                return Unauthorized(new { error = "Token de administrador inválido." });
            return null;
        }

        [HttpGet]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Listar(CancellationToken ct)
        {
            if (Negado() is { } negado) return negado;
            var catalogo = await _zapcar.ServicosAsync(ct);
            return Ok(new
            {
                teste = _settings.Value.Teste,
                configurado = _settings.Value.Configurado,
                servicos = await _precos.GradeAsync(catalogo, ct)
            });
        }

        public class PrecoInput
        {
            public string Servico { get; set; } = "";
            /// <summary>Null = valor para todos os planos.</summary>
            public int? PlanoId { get; set; }
            /// <summary>Null = apaga o valor fixo (volta para custo + acrescimo).</summary>
            public decimal? Preco { get; set; }
        }

        [HttpPut]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Definir([FromBody] PrecoInput input, CancellationToken ct)
        {
            if (Negado() is { } negado) return negado;
            var catalogo = await _zapcar.ServicosAsync(ct);
            var servico = catalogo.FirstOrDefault(s => s.Id == input.Servico);
            if (servico == null) return UnprocessableEntity(new { error = "Serviço desconhecido." });
            if (servico.PorUf.Count > 0) return UnprocessableEntity(new { error = "O CRLV varia por estado: o preço é sempre custo + acréscimo do plano." });
            if (input.Preco is <= 0) return UnprocessableEntity(new { error = "O preço precisa ser maior que zero (deixe vazio para usar o acréscimo do plano)." });
            if (input.Preco > 10000) return UnprocessableEntity(new { error = "Preço alto demais." });

            await _precos.DefinirAsync(servico.Id, input.PlanoId, input.Preco, ct);
            _logger.LogInformation("Preco da consulta {Servico} no plano {Plano} = {Preco}", servico.Id, input.PlanoId?.ToString() ?? "todos", input.Preco?.ToString() ?? "acrescimo");
            return Ok(new { servicos = await _precos.GradeAsync(catalogo, ct) });
        }
    }
}
