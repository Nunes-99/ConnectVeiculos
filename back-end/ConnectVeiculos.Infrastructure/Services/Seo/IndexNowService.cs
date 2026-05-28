using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Seo
{
    public class IndexNowService : IIndexNowService
    {
        private readonly HttpClient _httpClient;
        private readonly IndexNowSettings _settings;
        private readonly ILogger<IndexNowService> _logger;

        public IndexNowService(
            HttpClient httpClient,
            IOptions<IndexNowSettings> settings,
            ILogger<IndexNowService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task NotifyVeiculoAsync(string tenantSlug, int? veiculoId, CancellationToken ct = default)
        {
             if (!_settings.Enabled)
                 return;

             if (string.IsNullOrWhiteSpace(_settings.Key) || string.IsNullOrWhiteSpace(_settings.PublicSiteUrl))
             {
                 _logger.LogWarning("IndexNow habilitado mas Key/PublicSiteUrl vazios. Pulando notificacao.");
                 return;
             }

             if (string.IsNullOrWhiteSpace(tenantSlug))
                 return;

             var baseUrl = _settings.PublicSiteUrl.TrimEnd('/');
             var host = new Uri(baseUrl).Host;

             // Notifica sempre a home do catalogo (lista mudou) e, se houver,
             // a pagina especifica do veiculo. IndexNow aceita ate 10k URLs
             // por payload, entao 2 por vez e' insignificante.
             var urls = new List<string> { $"{baseUrl}/catalogo/{tenantSlug}" };
             if (veiculoId.HasValue)
                 urls.Add($"{baseUrl}/catalogo/{tenantSlug}/veiculo/{veiculoId.Value}");

             var payload = new
             {
                 host,
                 key = _settings.Key,
                 keyLocation = $"{baseUrl}/{_settings.Key}.txt",
                 urlList = urls
             };

             try
             {
                 var json = JsonSerializer.Serialize(payload);
                 var content = new StringContent(json, Encoding.UTF8, "application/json");
                 var response = await _httpClient.PostAsync("https://api.indexnow.org/indexnow", content, ct);

                 // 200 = aceito; 202 = aceito mas vai processar depois; 422 =
                 // chave invalida / sem prova de propriedade; 4xx geral = bug
                 // no payload. Loga ambos os casos sem propagar excecao
                 // (fire-and-forget — nao pode quebrar o cadastro de veiculo).
                 if (!response.IsSuccessStatusCode)
                 {
                     var body = await response.Content.ReadAsStringAsync(ct);
                     _logger.LogWarning(
                         "IndexNow rejeitou notificacao do veiculo {VeiculoId} do tenant {TenantSlug}: HTTP {Status} {Body}",
                         veiculoId, tenantSlug, (int)response.StatusCode, body);
                 }
                 else
                 {
                     _logger.LogDebug(
                         "IndexNow notificado: tenant {TenantSlug} veiculo {VeiculoId} ({UrlCount} URLs)",
                         tenantSlug, veiculoId, urls.Count);
                 }
             }
             catch (Exception ex)
             {
                 _logger.LogWarning(ex,
                     "Falha ao notificar IndexNow do veiculo {VeiculoId} do tenant {TenantSlug}",
                     veiculoId, tenantSlug);
             }
        }
    }
}
