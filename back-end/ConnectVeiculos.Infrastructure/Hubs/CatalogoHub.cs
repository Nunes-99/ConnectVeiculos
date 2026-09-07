using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Cache;
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
        private readonly ICacheService _cacheService;

        public CatalogoHubService(IHubContext<CatalogoHub> hubContext, ICacheService cacheService)
        {
            _hubContext = hubContext;
            _cacheService = cacheService;
        }

        public async Task NotificarAtualizacaoCatalogo(string tenantSlug, int lojaId, string tipoEvento, object dados)
        {
            var slug = string.IsNullOrWhiteSpace(tenantSlug) ? "default" : tenantSlug.ToLowerInvariant();
            var notificacao = new { tipo = tipoEvento, dados, timestamp = DateTime.UtcNow };

            // Derruba o cache do catalogo ANTES de avisar os clients. O
            // CatalogoController guarda o resultado por 1 minuto e nada invalidava
            // isso: o evento chegava na hora, o navegador refazia a busca na hora,
            // e a API devolvia a lista velha — carro novo levava ate 1 min pra
            // aparecer, com o selo "Em tempo real" na tela.
            //
            // Fica aqui, e nao nos use cases, por dois motivos: ICacheService vive
            // em Infrastructure e Application so referencia Core; e este metodo e'
            // o ponto por onde TODA mudanca do catalogo ja passa, entao nao da pra
            // esquecer de invalidar ao adicionar um fluxo novo.
            //
            // RemoveByPrefix pega as duas familias de chave do controller
            // ("catalogo_{lojaId}_..." e "catalogo_slug_{slug}_...") e o decorator
            // TenantAwareCacheService limita ao tenant atual.
            _cacheService.RemoveByPrefix(CacheKeys.Catalogo);

            // Notificar grupo da loja especifica (escopo por tenant)
            await _hubContext.Clients.Group($"tenant_{slug}_catalogo_loja_{lojaId}")
                .SendAsync("CatalogoAtualizado", notificacao);

            // Notificar grupo geral do tenant
            await _hubContext.Clients.Group($"tenant_{slug}_catalogo_geral")
                .SendAsync("CatalogoAtualizado", notificacao);
        }
    }
}
