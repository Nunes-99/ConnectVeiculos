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
        // Le o tenant do query string da conexao (ex: ?tenant=inova-motor).
        // Sem tenant = "default" (compatibilidade com clientes antigos / single-tenant).
        private string TenantSlug()
        {
            var http = Context.GetHttpContext();
            var slug = http?.Request.Query["tenant"].ToString();
            return string.IsNullOrWhiteSpace(slug) ? "default" : slug.ToLowerInvariant();
        }

        /// <summary>
        /// Entrar no grupo de uma loja para receber atualizacoes do catalogo
        /// </summary>
        public async Task AssinarLoja(int lojaId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{TenantSlug()}_catalogo_loja_{lojaId}");
        }

        /// <summary>
        /// Entrar no grupo geral para receber atualizacoes de todas as lojas do tenant
        /// </summary>
        public async Task AssinarCatalogoGeral()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{TenantSlug()}_catalogo_geral");
        }

        /// <summary>
        /// Sair do grupo de uma loja
        /// </summary>
        public async Task DesassinarLoja(int lojaId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant_{TenantSlug()}_catalogo_loja_{lojaId}");
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

        public async Task NotificarAtualizacaoCatalogo(string tenantSlug, int lojaId, string tipoEvento, object dados)
        {
            var slug = string.IsNullOrWhiteSpace(tenantSlug) ? "default" : tenantSlug.ToLowerInvariant();
            var notificacao = new { tipo = tipoEvento, dados, timestamp = DateTime.UtcNow };

            // Notificar grupo da loja especifica (escopo por tenant)
            await _hubContext.Clients.Group($"tenant_{slug}_catalogo_loja_{lojaId}")
                .SendAsync("CatalogoAtualizado", notificacao);

            // Notificar grupo geral do tenant
            await _hubContext.Clients.Group($"tenant_{slug}_catalogo_geral")
                .SendAsync("CatalogoAtualizado", notificacao);
        }
    }
}
