using ConnectVeiculos.Core.Entities.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectVeiculos.Infrastructure.Hubs
{
    /// <summary>
    /// Hub SignalR para notificacoes em tempo real.
    /// Multi-tenant: cada conexao entra em grupos prefixados com tenant_id —
    /// "tenant_{T}", "user_{T}_{U}" — para evitar broadcast cross-tenant.
    /// </summary>
    [Authorize]
    public class NotificacaoHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var (tenantId, userId) = GetTenantAndUserId();
            if (string.IsNullOrEmpty(tenantId))
            {
                // Token sem tenant_id — recusa conexao por seguranca
                Context.Abort();
                return;
            }

            // Grupos isolados por tenant
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{tenantId}_{userId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var (tenantId, userId) = GetTenantAndUserId();
            if (!string.IsNullOrEmpty(tenantId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
                if (!string.IsNullOrEmpty(userId))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{tenantId}_{userId}");
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinGroup(string groupName)
        {
            // Prefixa nomes de grupo customizado com tenant para evitar colisao
            var (tenantId, _) = GetTenantAndUserId();
            if (string.IsNullOrEmpty(tenantId)) return;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}:{groupName}");
        }

        public async Task LeaveGroup(string groupName)
        {
            var (tenantId, _) = GetTenantAndUserId();
            if (string.IsNullOrEmpty(tenantId)) return;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant_{tenantId}:{groupName}");
        }

        private (string? tenantId, string? userId) GetTenantAndUserId()
        {
            var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? Context.User?.FindFirst("UserId")?.Value;
            return (tenantId, userId);
        }
    }

    /// <summary>
    /// Servico para enviar notificacoes via SignalR (mantido para compatibilidade)
    /// </summary>
    public interface INotificacaoHubService : INotificacaoService
    {
    }

    /// <summary>
    /// Implementacao tenant-aware: descobre o tenant atual via ITenantContext
    /// (Scoped, populado pelo middleware ou pelo TenantScope dos jobs Hangfire).
    /// </summary>
    public class NotificacaoHubService : INotificacaoHubService
    {
        private readonly IHubContext<NotificacaoHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantContext _tenantContext;

        public NotificacaoHubService(
            IHubContext<NotificacaoHub> hubContext,
            IServiceScopeFactory scopeFactory,
            ITenantContext tenantContext)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
            _tenantContext = tenantContext;
        }

        private string TenantPrefix() => _tenantContext.IsResolved
            ? _tenantContext.TenantId.ToString()
            : throw new InvalidOperationException("NotificacaoHubService chamado sem tenant resolvido — broadcast cross-tenant bloqueado.");

        public async Task EnviarParaUsuarioAsync(int usuarioId, string tipo, object dados)
        {
            var tenantId = TenantPrefix();
            using var scope = _scopeFactory.CreateScope();
            var notificacaoRepo = scope.ServiceProvider.GetRequiredService<INotificacaoRepository>();

            var (titulo, mensagem) = GerarTituloMensagem(tipo, dados);
            var notificacao = CriarNotificacaoComTipo(usuarioId, titulo, mensagem, tipo);
            await notificacaoRepo.AddAsync(notificacao);

            await _hubContext.Clients.Group($"user_{tenantId}_{usuarioId}")
                .SendAsync("ReceberNotificacao", new { tipo, dados, timestamp = DateTime.UtcNow });
        }

        public async Task EnviarParaGrupoAsync(string grupo, string tipo, object dados)
        {
            var tenantId = TenantPrefix();
            await _hubContext.Clients.Group($"tenant_{tenantId}:{grupo}")
                .SendAsync("ReceberNotificacao", new { tipo, dados, timestamp = DateTime.UtcNow });
        }

        public async Task EnviarParaTodosAsync(string tipo, object dados)
        {
            var tenantId = TenantPrefix();
            using var scope = _scopeFactory.CreateScope();
            var notificacaoRepo = scope.ServiceProvider.GetRequiredService<INotificacaoRepository>();
            var usuarioRepo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();

            var usuarios = await usuarioRepo.GetAllAsync();
            var (titulo, mensagem) = GerarTituloMensagem(tipo, dados);
            var timestamp = DateTime.UtcNow;

            foreach (var usuario in usuarios)
            {
                var notificacao = CriarNotificacaoComTipo(usuario.UsuId, titulo, mensagem, tipo);
                await notificacaoRepo.AddAsync(notificacao);
            }

            // Broadcast restrito ao grupo do tenant — Clients.All vazaria entre tenants
            await _hubContext.Clients.Group($"tenant_{tenantId}")
                .SendAsync("ReceberNotificacao", new { tipo, dados, timestamp });
        }

        private static Notificacao CriarNotificacaoComTipo(int usuarioId, string titulo, string mensagem, string tipo)
        {
            return new Notificacao(
                0, usuarioId, titulo, mensagem, tipo, null, false, DateTime.UtcNow, null
            );
        }

        private static (string titulo, string mensagem) GerarTituloMensagem(string tipo, object dados)
        {
            var props = dados.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(dados)?.ToString() ?? "");

            return tipo switch
            {
                "NOVA_VENDA" => ("Nova Venda", $"Venda registrada: {props.GetValueOrDefault("veiculoNome", "Veículo")}"),
                "NOVO_VEICULO" => ("Novo Veículo", $"Veículo cadastrado: {props.GetValueOrDefault("marca", "")} {props.GetValueOrDefault("modelo", "")}"),
                "VEICULO_RESERVADO" => ("Veículo Reservado", $"{props.GetValueOrDefault("marca", "")} {props.GetValueOrDefault("modelo", "")} foi reservado"),
                "ESTORNO_VENDA" => ("Venda Estornada", $"Venda estornada: {props.GetValueOrDefault("veiculoNome", "Veículo")}"),
                _ => ("Notificação", "Nova notificação recebida")
            };
        }
    }
}
