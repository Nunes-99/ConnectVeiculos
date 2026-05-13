using ConnectVeiculos.Core.Entities.PushSubscriptions;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/push")]
    public class PushController : ControllerBase
    {
        private readonly ConnectVeiculosDbContext _context;
        private readonly IPushNotificationService _pushService;

        public PushController(ConnectVeiculosDbContext context, IPushNotificationService pushService)
        {
            _context = context;
            _pushService = pushService;
        }

        /// <summary>Devolve a chave publica VAPID para o cliente registrar a subscription</summary>
        [HttpGet("public-key")]
        [AllowAnonymous]
        public IActionResult GetPublicKey()
        {
            var key = _pushService.GetPublicKey();
            if (string.IsNullOrEmpty(key))
                return BadRequest(new { error = "Push notifications não configurado (VAPID_PUBLIC_KEY ausente)." });
            return Ok(new { publicKey = key });
        }

        /// <summary>Registra uma nova subscription do navegador</summary>
        [HttpPost("subscribe")]
        [Authorize]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
        {
            if (string.IsNullOrEmpty(request.Endpoint) || request.Keys?.P256dh == null || request.Keys?.Auth == null)
                return BadRequest("Subscription invalida.");

            var existente = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s => s.PsbEndpoint == request.Endpoint);
            if (existente != null) return Ok(new { id = existente.PsbId, mensagem = "Ja registrada." });

            int? usuId = null;
            var usuClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(usuClaim, out var parsed)) usuId = parsed;

            var sub = new PushSubscription(0, usuId, request.Endpoint, request.Keys.P256dh, request.Keys.Auth, request.UserAgent ?? "");
            _context.PushSubscriptions.Add(sub);
            await _context.SaveChangesAsync();

            return Ok(new { id = sub.PsbId, mensagem = "Push subscription registrada." });
        }

        /// <summary>Remove subscription</summary>
        [HttpDelete("unsubscribe")]
        [AllowAnonymous]
        public async Task<IActionResult> Unsubscribe([FromQuery] string endpoint)
        {
            var sub = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.PsbEndpoint == endpoint);
            if (sub == null) return NotFound();
            _context.PushSubscriptions.Remove(sub);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>Disparo de teste (admin)</summary>
        [HttpPost("test")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Test([FromBody] TestPushRequest request)
        {
            await _pushService.EnviarParaTodosAdminAsync(
                request.Titulo ?? "Teste ConnectVeiculos",
                request.Corpo ?? "Notificacao push funcionando!",
                request.Url);
            return Ok(new { mensagem = "Teste enviado." });
        }
    }

    public class SubscribeRequest
    {
        public string Endpoint { get; set; } = "";
        public SubscribeKeys? Keys { get; set; }
        public string? UserAgent { get; set; }
    }

    public class SubscribeKeys
    {
        public string P256dh { get; set; } = "";
        public string Auth { get; set; } = "";
    }

    public class TestPushRequest
    {
        public string? Titulo { get; set; }
        public string? Corpo { get; set; }
        public string? Url { get; set; }
    }
}
