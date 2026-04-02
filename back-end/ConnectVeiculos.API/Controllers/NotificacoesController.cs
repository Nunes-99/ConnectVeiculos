using System.Security.Claims;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using ConnectVeiculos.Infrastructure.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para gerenciamento de notificacoes
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    [EnableRateLimiting("api")]
    public class NotificacoesController : ControllerBase
    {
        private readonly INotificacaoRepository _notificacaoRepository;
        private readonly INotificacaoHubService _notificacaoHubService;

        public NotificacoesController(
            INotificacaoRepository notificacaoRepository,
            INotificacaoHubService notificacaoHubService)
        {
            _notificacaoRepository = notificacaoRepository;
            _notificacaoHubService = notificacaoHubService;
        }

        /// <summary>
        /// Lista as notificacoes do usuario autenticado
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar([FromQuery] bool apenasNaoLidas = false)
        {
            var userId = GetUserId();
            var notificacoes = await _notificacaoRepository.GetByUsuarioIdAsync(userId, apenasNaoLidas);
            return Ok(notificacoes);
        }

        /// <summary>
        /// Retorna a quantidade de notificacoes nao lidas
        /// </summary>
        [HttpGet("nao-lidas/count")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ContarNaoLidas()
        {
            var userId = GetUserId();
            var count = await _notificacaoRepository.GetCountNaoLidasAsync(userId);
            return Ok(new { count });
        }

        /// <summary>
        /// Marca uma notificacao como lida
        /// </summary>
        [HttpPost("{id}/marcar-lida")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> MarcarComoLida(int id)
        {
            await _notificacaoRepository.MarcarComoLidaAsync(id);
            return Ok(new { mensagem = "Notificacao marcada como lida." });
        }

        /// <summary>
        /// Marca todas as notificacoes do usuario como lidas
        /// </summary>
        [HttpPost("marcar-todas-lidas")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> MarcarTodasComoLidas()
        {
            var userId = GetUserId();
            await _notificacaoRepository.MarcarTodasComoLidasAsync(userId);
            return Ok(new { mensagem = "Todas as notificacoes foram marcadas como lidas." });
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }
    }
}
