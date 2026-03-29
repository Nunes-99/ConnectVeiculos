using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace ConnectVeiculos.Infrastructure.Hubs
{
    /// <summary>
    /// Hub SignalR publico para atualizacoes do catalogo em tempo real.
    /// Nao requer autenticacao - qualquer visitante pode se inscrever.
    /// </summary>
    public class CatalogoHub : Hub
    {
        /// <summary>
        /// Entrar no grupo de uma loja para receber atualizacoes do catalogo
        /// </summary>
        public async Task AssinarLoja(int lojaId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"catalogo_loja_{lojaId}");
        }

        /// <summary>
        /// Entrar no grupo geral para receber atualizacoes de todas as lojas
        /// </summary>
        public async Task AssinarCatalogoGeral()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "catalogo_geral");
        }

        /// <summary>
        /// Sair do grupo de uma loja
        /// </summary>
        public async Task DesassinarLoja(int lojaId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"catalogo_loja_{lojaId}");
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }

    public class CatalogoHubService : ICatalogoHubService
    {
        private readonly IHubContext<CatalogoHub> _hubContext;

        public CatalogoHubService(IHubContext<CatalogoHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotificarAtualizacaoCatalogo(int lojaId, string tipoEvento, object dados)
        {
            var notificacao = new { tipo = tipoEvento, dados, timestamp = DateTime.UtcNow };

            // Notificar grupo da loja especifica
            await _hubContext.Clients.Group($"catalogo_loja_{lojaId}")
                .SendAsync("CatalogoAtualizado", notificacao);

            // Notificar grupo geral
            await _hubContext.Clients.Group("catalogo_geral")
                .SendAsync("CatalogoAtualizado", notificacao);
        }
    }
}
