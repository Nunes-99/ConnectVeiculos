using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LibPushSubscription = Lib.Net.Http.WebPush.PushSubscription;

namespace ConnectVeiculos.Infrastructure.Services.Push
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly ConnectVeiculosDbContext _context;
        private readonly PushServiceClient _pushClient;
        private readonly VapidAuthentication? _vapid;
        private readonly string _publicKey;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(
            ConnectVeiculosDbContext context,
            IConfiguration configuration,
            ILogger<PushNotificationService> logger)
        {
            _context = context;
            _logger = logger;
            _pushClient = new PushServiceClient();

            var publicKey = Environment.GetEnvironmentVariable("VAPID_PUBLIC_KEY") ?? configuration["Vapid:PublicKey"] ?? "";
            var privateKey = Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY") ?? configuration["Vapid:PrivateKey"] ?? "";
            var subject = Environment.GetEnvironmentVariable("VAPID_SUBJECT") ?? configuration["Vapid:Subject"] ?? "mailto:contato@connectveiculos.dev.br";

            _publicKey = publicKey;
            if (!string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(privateKey))
            {
                _vapid = new VapidAuthentication(publicKey, privateKey) { Subject = subject };
                _pushClient.DefaultAuthentication = _vapid;
            }
        }

        public string GetPublicKey() => _publicKey;

        public async Task EnviarParaUsuarioAsync(int usuarioId, string titulo, string corpo, string? url = null)
        {
            if (_vapid == null)
            {
                _logger.LogWarning("Push nao enviado: VAPID nao configurado");
                return;
            }

            var subs = await _context.PushSubscriptions.AsNoTracking()
                .Where(s => s.R_UsuId == usuarioId).ToListAsync();
            await EnviarParaListaAsync(subs, titulo, corpo, url);
        }

        public async Task EnviarParaTodosAdminAsync(string titulo, string corpo, string? url = null)
        {
            if (_vapid == null)
            {
                _logger.LogWarning("Push nao enviado: VAPID nao configurado");
                return;
            }

            var subs = await _context.PushSubscriptions.AsNoTracking()
                .Where(s => s.R_UsuId != null).ToListAsync();
            await EnviarParaListaAsync(subs, titulo, corpo, url);
        }

        private async Task EnviarParaListaAsync(
            List<Core.Entities.PushSubscriptions.PushSubscription> subs,
            string titulo, string corpo, string? url)
        {
            var payload = JsonSerializer.Serialize(new
            {
                notification = new { title = titulo, body = corpo, icon = "/icons/icon-192x192.png", data = new { url = url ?? "/" } }
            });

            foreach (var s in subs)
            {
                try
                {
                    var lib = new LibPushSubscription { Endpoint = s.PsbEndpoint };
                    lib.SetKey(PushEncryptionKeyName.P256DH, s.PsbP256dh);
                    lib.SetKey(PushEncryptionKeyName.Auth, s.PsbAuth);
                    await _pushClient.RequestPushMessageDeliveryAsync(lib, new PushMessage(payload));
                }
                catch (Exception ex) when (ex.Message.Contains("410") || ex.Message.Contains("404"))
                {
                    // Subscription expirada/invalida - remover
                    var stale = await _context.PushSubscriptions.FirstOrDefaultAsync(x => x.PsbId == s.PsbId);
                    if (stale != null) { _context.PushSubscriptions.Remove(stale); await _context.SaveChangesAsync(); }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao enviar push para subscription {Id}", s.PsbId);
                }
            }
        }
    }
}
