using ConnectVeiculos.Core.Interfaces.Database.Repositories.Logs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para logs de auditoria
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class LogsController : ControllerBase
    {
        private readonly ILogAuditoriaRepository _logRepository;

        public LogsController(ILogAuditoriaRepository logRepository)
        {
            _logRepository = logRepository;
        }

        /// <summary>
        /// Retorna logs de auditoria paginados
        /// </summary>
        [HttpGet("paged")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarPaginado(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? tabela = null,
            [FromQuery] string? acao = null,
            [FromQuery] DateTime? dataInicio = null,
            [FromQuery] DateTime? dataFim = null)
        {
            var (items, total) = await _logRepository.ConsultarPaginadoAsync(page, pageSize, tabela, acao, dataInicio, dataFim);

            var itemsList = items.Select(l => new
            {
                l.LogId,
                l.LogTabela,
                l.LogAcao,
                l.LogRegistroId,
                l.LogUsuarioId,
                l.LogUsuarioNome,
                l.LogDataHora,
                l.LogIP
            }).ToList();

            var result = new
            {
                Items = itemsList,
                TotalItems = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0
            };

            return Ok(result);
        }

        /// <summary>
        /// Retorna detalhes de um log especifico
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ObterPorId(int id)
        {
            var log = await _logRepository.ObterPorIdAsync(id);

            if (log == null)
                return NotFound();

            return Ok(new
            {
                log.LogId,
                log.LogTabela,
                log.LogAcao,
                log.LogRegistroId,
                log.LogDadosAntigos,
                log.LogDadosNovos,
                log.LogUsuarioId,
                log.LogUsuarioNome,
                log.LogDataHora,
                log.LogIP
            });
        }

        /// <summary>
        /// Retorna as tabelas disponiveis para filtro
        /// </summary>
        [HttpGet("tabelas")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ObterTabelas()
        {
            var tabelas = new[]
            {
                "Usuario",
                "Veiculo",
                "Loja",
                "Categoria",
                "Acesso",
                "Venda"
            };

            return Ok(tabelas);
        }

        /// <summary>
        /// Retorna as acoes disponiveis para filtro
        /// </summary>
        [HttpGet("acoes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ObterAcoes()
        {
            var acoes = new[]
            {
                "INSERT",
                "UPDATE",
                "DELETE"
            };

            return Ok(acoes);
        }
    }
}
