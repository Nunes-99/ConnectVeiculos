using System.Net.Http.Headers;
using System.Net.Http.Json;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.WhatsApp
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IConfiguracaoSistemaRepository _configRepository;
        private readonly ILogger<WhatsAppService> _logger;
        private const string BaseUrl = "https://graph.facebook.com/v21.0";

        private const string KEY_TOKEN = "WHATSAPP_ACCESS_TOKEN";
        private const string KEY_PHONE = "WHATSAPP_PHONE_ID";
        private const string KEY_VERIFY = "WHATSAPP_VERIFY_TOKEN";

        public WhatsAppService(
            HttpClient httpClient,
            IConfiguration configuration,
            IConfiguracaoSistemaRepository configRepository,
            ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _configRepository = configRepository;
            _logger = logger;
        }

        // Precedencia: env var > banco > appsettings
        private async Task<(string token, string phoneId)> ResolveCredentialsAsync()
        {
            var token = Environment.GetEnvironmentVariable("WHATSAPP_ACCESS_TOKEN");
            if (string.IsNullOrEmpty(token)) token = await _configRepository.GetValorAsync(KEY_TOKEN);
            if (string.IsNullOrEmpty(token)) token = _configuration["WhatsApp:AccessToken"] ?? "";

            var phoneId = Environment.GetEnvironmentVariable("WHATSAPP_PHONE_ID");
            if (string.IsNullOrEmpty(phoneId)) phoneId = await _configRepository.GetValorAsync(KEY_PHONE);
            if (string.IsNullOrEmpty(phoneId)) phoneId = _configuration["WhatsApp:PhoneId"] ?? "";

            return (token ?? "", phoneId ?? "");
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var (token, phoneId) = await ResolveCredentialsAsync();
            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(phoneId);
        }

        public async Task<WhatsAppConfigInfo> GetConfigAsync()
        {
            var (token, phoneId) = await ResolveCredentialsAsync();
            var verify = await GetVerifyTokenAsync();
            return new WhatsAppConfigInfo
            {
                Configurado = !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(phoneId),
                PhoneId = string.IsNullOrEmpty(phoneId) ? null : phoneId,
                VerifyTokenDefinido = !string.IsNullOrEmpty(verify)
            };
        }

        public async Task SalvarConfigAsync(string accessToken, string phoneId, string verifyToken)
        {
            await _configRepository.SetValorAsync(KEY_TOKEN, accessToken ?? "");
            await _configRepository.SetValorAsync(KEY_PHONE, phoneId ?? "");
            await _configRepository.SetValorAsync(KEY_VERIFY, verifyToken ?? "");
            _logger.LogInformation("WhatsApp config salva (phoneId={PhoneId})", phoneId);
        }

        public async Task DesconectarAsync()
        {
            await _configRepository.SetValorAsync(KEY_TOKEN, "");
            await _configRepository.SetValorAsync(KEY_PHONE, "");
            await _configRepository.SetValorAsync(KEY_VERIFY, "");
            _logger.LogInformation("WhatsApp desconectado.");
        }

        public async Task<string?> GetVerifyTokenAsync()
        {
            var fromEnv = Environment.GetEnvironmentVariable("WHATSAPP_VERIFY_TOKEN");
            if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;
            var fromDb = await _configRepository.GetValorAsync(KEY_VERIFY);
            if (!string.IsNullOrEmpty(fromDb)) return fromDb;
            return _configuration["WhatsApp:VerifyToken"];
        }

        public async Task<bool> EnviarMensagemAsync(string telefoneE164, string mensagem)
        {
            var (token, phoneId) = await ResolveCredentialsAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(phoneId))
            {
                _logger.LogWarning("WhatsApp nao configurado.");
                return false;
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                to = NormalizarTelefone(telefoneE164),
                type = "text",
                text = new { body = mensagem }
            };

            return await PostAsync(payload, token, phoneId);
        }

        public async Task<bool> EnviarTemplateAsync(string telefoneE164, string templateName, string lang, IEnumerable<string> parametros)
        {
            var (token, phoneId) = await ResolveCredentialsAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(phoneId))
            {
                _logger.LogWarning("WhatsApp nao configurado.");
                return false;
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                to = NormalizarTelefone(telefoneE164),
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = lang },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = parametros.Select(p => new { type = "text", text = p }).ToArray()
                        }
                    }
                }
            };

            return await PostAsync(payload, token, phoneId);
        }

        private async Task<bool> PostAsync(object payload, string token, string phoneId)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{phoneId}/messages");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Content = JsonContent.Create(payload);

                var resp = await _httpClient.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("WhatsApp falhou ({Status}): {Body}", resp.StatusCode, body);
                    return false;
                }

                _logger.LogInformation("WhatsApp enviado com sucesso");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar WhatsApp");
                return false;
            }
        }

        private static string NormalizarTelefone(string telefone)
        {
            var digits = new string(telefone.Where(char.IsDigit).ToArray());
            if (!digits.StartsWith("55") && digits.Length == 10) digits = "55" + digits;
            if (!digits.StartsWith("55") && digits.Length == 11) digits = "55" + digits;
            return digits;
        }
    }
}
