using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Webhooks;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Webhook
{
    public class WebhookService : IWebhookService
    {
        private readonly IWebhookRepository _webhookRepository;
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebhookService> _logger;

        public WebhookService(
            IWebhookRepository webhookRepository,
            HttpClient httpClient,
            ILogger<WebhookService> logger)
        {
            _webhookRepository = webhookRepository;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task DispararAsync(string evento, object payload)
        {
            await DispararAsync<object>(evento, payload);
        }

        public async Task DispararAsync<T>(string evento, T payload) where T : class
        {
            var webhooks = await _webhookRepository.GetActiveByEventoAsync(evento);

            foreach (var webhook in webhooks)
            {
                _ = Task.Run(async () =>
                {
                    await EnviarWebhookAsync(webhook, evento, payload);
                });
            }
        }

        private async Task EnviarWebhookAsync<T>(Core.Entities.Webhooks.Webhook webhook, string evento, T payload)
        {
            try
            {
                var body = new
                {
                    evento,
                    timestamp = DateTime.UtcNow,
                    payload
                };

                var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var request = new HttpRequestMessage(HttpMethod.Post, webhook.WebUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                // Adicionar assinatura HMAC
                var signature = GerarAssinaturaHMAC(json, webhook.WebSecret);
                request.Headers.Add("X-Webhook-Signature", signature);
                request.Headers.Add("X-Webhook-Event", evento);

                var response = await _httpClient.SendAsync(request);

                webhook.RegistrarExecucao(response.IsSuccessStatusCode);
                await _webhookRepository.UpdateAsync(webhook);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Webhook enviado com sucesso. URL: {Url}, Evento: {Evento}",
                        webhook.WebUrl, evento);
                }
                else
                {
                    _logger.LogWarning(
                        "Webhook falhou. URL: {Url}, Evento: {Evento}, Status: {Status}",
                        webhook.WebUrl, evento, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                webhook.RegistrarExecucao(false);
                await _webhookRepository.UpdateAsync(webhook);

                _logger.LogError(ex,
                    "Erro ao enviar webhook. URL: {Url}, Evento: {Evento}",
                    webhook.WebUrl, evento);
            }
        }

        private static string GerarAssinaturaHMAC(string payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
        }
    }
}
