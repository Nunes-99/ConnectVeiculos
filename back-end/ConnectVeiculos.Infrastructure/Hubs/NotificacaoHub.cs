using ConnectVeiculos.Core.Entities.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

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
        private readonly IServiceScopeFactory _scopeFactory;

        public NotificacaoHubService(
            IHubContext<NotificacaoHub> hubContext,
            IServiceScopeFactory scopeFactory)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        public async Task EnviarParaUsuarioAsync(int usuarioId, string tipo, object dados)
        {
            using var scope = _scopeFactory.CreateScope();
            var notificacaoRepo = scope.ServiceProvider.GetRequiredService<INotificacaoRepository>();

            var (titulo, mensagem) = GerarTituloMensagem(tipo, dados);
            var notificacao = CriarNotificacaoComTipo(usuarioId, titulo, mensagem, tipo);
            await notificacaoRepo.AddAsync(notificacao);

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

            await _hubContext.Clients.All
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
