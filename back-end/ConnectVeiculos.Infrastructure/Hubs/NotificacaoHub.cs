using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectVeiculos.Infrastructure.Hubs
{
    /// <summary>
    /// Hub SignalR para notificacoes em tempo real
    /// </summary>
    [Authorize]
    public class NotificacaoHub : Hub
    {
        /// <summary>
        /// Chamado quando um cliente se conecta
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst("UserId")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Chamado quando um cliente se desconecta
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User?.FindFirst("UserId")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Entrar em um grupo (para notificacoes de loja especifica)
        /// </summary>
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Sair de um grupo
        /// </summary>
        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
    }

    /// <summary>
    /// Servico para enviar notificacoes via SignalR (mantido para compatibilidade)
    /// </summary>
    public interface INotificacaoHubService : INotificacaoService
    {
    }

    public class NotificacaoHubService : INotificacaoHubService
    {
        private readonly IHubContext<NotificacaoHub> _hubContext;

        public NotificacaoHubService(IHubContext<NotificacaoHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task EnviarParaUsuarioAsync(int usuarioId, string tipo, object dados)
        {
            await _hubContext.Clients.Group($"user_{usuarioId}")
                .SendAsync("ReceberNotificacao", new { tipo, dados, timestamp = DateTime.UtcNow });
        }

        public async Task EnviarParaGrupoAsync(string grupo, string tipo, object dados)
        {
            await _hubContext.Clients.Group(grupo)
                .SendAsync("ReceberNotificacao", new { tipo, dados, timestamp = DateTime.UtcNow });
        }

        public async Task EnviarParaTodosAsync(string tipo, object dados)
        {
            await _hubContext.Clients.All
                .SendAsync("ReceberNotificacao", new { tipo, dados, timestamp = DateTime.UtcNow });
        }
    }
}
