using System.Net.Http.Json;
using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Meta
{
    public class MetaOAuthService : IMetaOAuthService
    {
        // Keys persistidas em ConfiguracaoSistema (por-tenant — banco SQLite do tenant).
        // Tokens (META_USER_TOKEN, META_PAGE_TOKEN) sao cifrados via ITokenProtector.
        public const string KEY_USER_TOKEN_CIFRADO = "META_USER_TOKEN_CIFRADO";
        public const string KEY_USER_TOKEN_EXPIRA = "META_USER_TOKEN_EXPIRA";
        public const string KEY_PAGE_ID = "META_PAGE_ID";
        public const string KEY_PAGE_NOME = "META_PAGE_NOME";
        public const string KEY_PAGE_TOKEN_CIFRADO = "META_PAGE_TOKEN_CIFRADO";
        public const string KEY_IG_BUSINESS_ID = "META_IG_BUSINESS_ID";
        public const string KEY_IG_USERNAME = "META_IG_USERNAME";

        // Escopos: pages_show_list pra listar; manage_posts pra publicar na timeline;
        // instagram_basic + content_publish pra IG; catalog_management pra Catalog.
        private const string SCOPES =
            "pages_show_list,pages_read_engagement,pages_manage_posts," +
            "instagram_basic,instagram_content_publish," +
            "catalog_management,business_management";

        private readonly HttpClient _httpClient;
        private readonly MetaSettings _settings;
        private readonly IConfiguracaoSistemaRepository _configRepository;
        private readonly ITokenProtector _tokenProtector;
        private readonly IOAuthStateProtector _stateProtector;
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<MetaOAuthService> _logger;

        public MetaOAuthService(
            HttpClient httpClient,
            IOptions<MetaSettings> settings,
            IConfiguracaoSistemaRepository configRepository,
            ITokenProtector tokenProtector,
            IOAuthStateProtector stateProtector,
            ITenantContext tenantContext,
            ILogger<MetaOAuthService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _configRepository = configRepository;
            _tokenProtector = tokenProtector;
            _stateProtector = stateProtector;
            _tenantContext = tenantContext;
            _logger = logger;
        }

        public Task<string> BuildAuthorizeUrlAsync(string redirectUri)
        {
            if (string.IsNullOrWhiteSpace(_settings.AppId))
                throw new InvalidOperationException(
                    "Meta nao configurado: defina MetaSettings__AppId via env var.");

            var tenantSlug = _tenantContext.IsResolved ? _tenantContext.TenantSlug : string.Empty;
            var state = _stateProtector.Proteger(tenantSlug);

            var url = $"https://www.facebook.com/{_settings.ApiVersion}/dialog/oauth" +
                      $"?client_id={_settings.AppId}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&scope={Uri.EscapeDataString(SCOPES)}" +
                      $"&state={Uri.EscapeDataString(state)}" +
                      $"&response_type=code";

            return Task.FromResult(url);
        }

        public async Task<MetaOAuthCallbackResult> ExchangeCodeAsync(string code, string state, string redirectUri)
        {
            var tenantSlug = _tenantContext.IsResolved ? _tenantContext.TenantSlug : string.Empty;
            _stateProtector.Validar(state, tenantSlug);

            if (string.IsNullOrWhiteSpace(_settings.AppId) || string.IsNullOrWhiteSpace(_settings.AppSecret))
                return new MetaOAuthCallbackResult { Sucesso = false, Mensagem = "App Meta nao configurado (AppId/AppSecret)." };

            // Step 1: code -> short-lived user token (~1-2h).
            var shortUrl = $"https://graph.facebook.com/{_settings.ApiVersion}/oauth/access_token" +
                           $"?client_id={_settings.AppId}" +
                           $"&client_secret={_settings.AppSecret}" +
                           $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                           $"&code={Uri.EscapeDataString(code)}";

            var shortResp = await _httpClient.GetAsync(shortUrl);
            var shortBody = await shortResp.Content.ReadAsStringAsync();
            if (!shortResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meta exchange code falhou: {Body}", shortBody);
                return new MetaOAuthCallbackResult { Sucesso = false, Mensagem = $"Falha ao trocar code por token: {shortBody}" };
            }

            string? shortToken;
            using (var doc = JsonDocument.Parse(shortBody))
            {
                shortToken = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            }
            if (string.IsNullOrEmpty(shortToken))
                return new MetaOAuthCallbackResult { Sucesso = false, Mensagem = "Token nao retornado pela Meta." };

            // Step 2: short-lived -> long-lived user token (60 dias).
            var longUrl = $"https://graph.facebook.com/{_settings.ApiVersion}/oauth/access_token" +
                          $"?grant_type=fb_exchange_token" +
                          $"&client_id={_settings.AppId}" +
                          $"&client_secret={_settings.AppSecret}" +
                          $"&fb_exchange_token={Uri.EscapeDataString(shortToken)}";

            var longResp = await _httpClient.GetAsync(longUrl);
            var longBody = await longResp.Content.ReadAsStringAsync();
            if (!longResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meta long-lived exchange falhou: {Body}", longBody);
                return new MetaOAuthCallbackResult { Sucesso = false, Mensagem = $"Falha no long-lived token: {longBody}" };
            }

            string longToken;
            int expiresIn;
            using (var doc = JsonDocument.Parse(longBody))
            {
                longToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
                expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 5183999; // ~60d
            }

            // Step 3: descobre nome do usuario (so pra UX).
            string userNome = "";
            try
            {
                var meResp = await _httpClient.GetAsync(
                    $"https://graph.facebook.com/{_settings.ApiVersion}/me?fields=name&access_token={Uri.EscapeDataString(longToken)}");
                if (meResp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await meResp.Content.ReadAsStringAsync());
                    userNome = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Meta /me opcional falhou (ignorado)"); }

            // Step 4: lista pages so pra contar (lista completa vai pra UI via ListarPagesAsync).
            int pagesCount = 0;
            try
            {
                var pages = await ListarPagesUsandoTokenAsync(longToken);
                pagesCount = pages.Count;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Meta listar pages no callback falhou (continuando)"); }

            // Step 5: persiste.
            await _configRepository.SetValorAsync(KEY_USER_TOKEN_CIFRADO, _tokenProtector.Protect(longToken));
            await _configRepository.SetValorAsync(
                KEY_USER_TOKEN_EXPIRA,
                DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"));

            return new MetaOAuthCallbackResult
            {
                Sucesso = true,
                Mensagem = "Conectado com sucesso.",
                UserNome = userNome,
                PagesEncontradas = pagesCount
            };
        }

        public async Task<IReadOnlyList<MetaPageOption>> ListarPagesAsync()
        {
            var userToken = await GetUserTokenAsync();
            if (string.IsNullOrEmpty(userToken))
                return Array.Empty<MetaPageOption>();
            return await ListarPagesUsandoTokenAsync(userToken);
        }

        private async Task<List<MetaPageOption>> ListarPagesUsandoTokenAsync(string userToken)
        {
            var url = $"https://graph.facebook.com/{_settings.ApiVersion}/me/accounts" +
                      $"?fields=id,name,category,instagram_business_account{{id,username}}" +
                      $"&access_token={Uri.EscapeDataString(userToken)}";

            var resp = await _httpClient.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meta listar pages falhou: {Body}", body);
                return new List<MetaPageOption>();
            }

            var pages = new List<MetaPageOption>();
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return pages;

            foreach (var el in data.EnumerateArray())
            {
                var p = new MetaPageOption
                {
                    PageId = el.GetProperty("id").GetString() ?? "",
                    Nome = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Categoria = el.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "",
                };
                if (el.TryGetProperty("instagram_business_account", out var ig) && ig.ValueKind != JsonValueKind.Null)
                {
                    p.TemInstagramBusiness = true;
                    p.InstagramUsername = ig.TryGetProperty("username", out var u) ? u.GetString() : null;
                }
                pages.Add(p);
            }
            return pages;
        }

        public async Task<MetaSelectPageResult> SelecionarPageAsync(string pageId)
        {
            if (string.IsNullOrWhiteSpace(pageId))
                return new MetaSelectPageResult { Sucesso = false, Mensagem = "PageId obrigatorio." };

            var userToken = await GetUserTokenAsync();
            if (string.IsNullOrEmpty(userToken))
                return new MetaSelectPageResult { Sucesso = false, Mensagem = "User token Meta ausente — refaca o login." };

            // Page Access Token derivado do User Token: nao expira (Page Token herda
            // do user token long-lived; renova quando o user token renovar).
            var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}" +
                      $"?fields=id,name,access_token,instagram_business_account{{id,username}}" +
                      $"&access_token={Uri.EscapeDataString(userToken)}";

            var resp = await _httpClient.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meta select page falhou: {Body}", body);
                return new MetaSelectPageResult { Sucesso = false, Mensagem = $"Falha ao obter Page Token: {body}" };
            }

            using var doc = JsonDocument.Parse(body);
            var pageNome = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var pageToken = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(pageToken))
                return new MetaSelectPageResult { Sucesso = false, Mensagem = "Resposta nao incluiu Page Access Token (verifique permissoes do app)." };

            string? igId = null;
            string? igUsername = null;
            if (doc.RootElement.TryGetProperty("instagram_business_account", out var ig) && ig.ValueKind != JsonValueKind.Null)
            {
                igId = ig.TryGetProperty("id", out var igIdEl) ? igIdEl.GetString() : null;
                igUsername = ig.TryGetProperty("username", out var igUserEl) ? igUserEl.GetString() : null;
            }

            await _configRepository.SetValorAsync(KEY_PAGE_ID, pageId);
            await _configRepository.SetValorAsync(KEY_PAGE_NOME, pageNome);
            await _configRepository.SetValorAsync(KEY_PAGE_TOKEN_CIFRADO, _tokenProtector.Protect(pageToken));
            await _configRepository.SetValorAsync(KEY_IG_BUSINESS_ID, igId ?? "");
            await _configRepository.SetValorAsync(KEY_IG_USERNAME, igUsername ?? "");

            return new MetaSelectPageResult
            {
                Sucesso = true,
                Mensagem = "Page selecionada e tokens salvos.",
                PageId = pageId,
                PageNome = pageNome,
                InstagramConectado = !string.IsNullOrEmpty(igId),
                InstagramUsername = igUsername
            };
        }

        public async Task<MetaConnectionInfo> GetConnectionInfoAsync()
        {
            var userTokenCifrado = await _configRepository.GetValorAsync(KEY_USER_TOKEN_CIFRADO);
            var expiraStr = await _configRepository.GetValorAsync(KEY_USER_TOKEN_EXPIRA);
            var pageId = await _configRepository.GetValorAsync(KEY_PAGE_ID);
            var pageNome = await _configRepository.GetValorAsync(KEY_PAGE_NOME);
            var igId = await _configRepository.GetValorAsync(KEY_IG_BUSINESS_ID);
            var igUsername = await _configRepository.GetValorAsync(KEY_IG_USERNAME);

            DateTime? expira = null;
            if (!string.IsNullOrEmpty(expiraStr) && DateTime.TryParse(expiraStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var d))
                expira = d;

            return new MetaConnectionInfo
            {
                UserTokenDefinido = !string.IsNullOrEmpty(userTokenCifrado),
                PageSelecionada = !string.IsNullOrEmpty(pageId),
                PageId = string.IsNullOrEmpty(pageId) ? null : pageId,
                PageNome = string.IsNullOrEmpty(pageNome) ? null : pageNome,
                InstagramConectado = !string.IsNullOrEmpty(igId),
                InstagramBusinessId = string.IsNullOrEmpty(igId) ? null : igId,
                InstagramUsername = string.IsNullOrEmpty(igUsername) ? null : igUsername,
                UserTokenExpiraEm = expira
            };
        }

        public async Task DesconectarAsync()
        {
            await _configRepository.SetValorAsync(KEY_USER_TOKEN_CIFRADO, "");
            await _configRepository.SetValorAsync(KEY_USER_TOKEN_EXPIRA, "");
            await _configRepository.SetValorAsync(KEY_PAGE_ID, "");
            await _configRepository.SetValorAsync(KEY_PAGE_NOME, "");
            await _configRepository.SetValorAsync(KEY_PAGE_TOKEN_CIFRADO, "");
            await _configRepository.SetValorAsync(KEY_IG_BUSINESS_ID, "");
            await _configRepository.SetValorAsync(KEY_IG_USERNAME, "");
        }

        // Helper publico interno: retorna o user token decifrado (ou string vazia).
        // Page/Instagram services chamam direto via DI tambem se precisarem,
        // mas eles usam o KEY_PAGE_TOKEN_CIFRADO (Page Token) que nao expira.
        public async Task<string> GetUserTokenAsync()
        {
            var cifrado = await _configRepository.GetValorAsync(KEY_USER_TOKEN_CIFRADO);
            if (string.IsNullOrEmpty(cifrado)) return "";
            try { return _tokenProtector.Unprotect(cifrado); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Meta user token nao pode ser decifrado (chave rotacionada?)");
                return "";
            }
        }
    }
}
