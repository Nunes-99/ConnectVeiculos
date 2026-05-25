using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.MercadoLivre
{
    public class MercadoLivreService : IMercadoLivreService
    {
        private readonly HttpClient _httpClient;
        private readonly MercadoLivreSettings _settings;
        private readonly ILogger<MercadoLivreService> _logger;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly IConfiguracaoSistemaRepository _configRepository;
         private readonly ITokenProtector _tokenProtector;
         private readonly IOAuthStateProtector _stateProtector;
         private readonly ITenantContext _tenantContext;
         private readonly IIntegracaoMercadoLivreRepository _integracaoRepo;
         private readonly IIntegracaoLogRepository _logRepo;
         private readonly IVeiculoPublicacaoRepository _publicacaoRepo;

        private const string BaseUrl = "https://api.mercadolibre.com";
        private const string AuthUrl = "https://auth.mercadolivre.com.br/authorization";

         // Skew aplicado ao expires_in: dispara refresh proativo X segundos antes
         // de expirar pra cobrir latencia de rede + clock drift + tempo da chamada.
         private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

         // Lock por tenant para serializar refreshes concorrentes. ML invalida o
         // refresh_token a CADA refresh (rolling tokens) — duas threads pedindo
         // refresh simultaneamente da mesma integracao fariam uma delas perder o
         // token novo e ficar autenticando com o antigo (invalid_grant). Estatico
         // porque queremos um lock global por tenant, nao por scope da request.
         // ConcurrentDictionary aceita crescimento: 1 SemaphoreSlim por tenant ativo.
         private static readonly ConcurrentDictionary<string, SemaphoreSlim> RefreshLocks = new();

        public MercadoLivreService(
            HttpClient httpClient,
            IOptions<MercadoLivreSettings> settings,
            ILogger<MercadoLivreService> logger,
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            ILojaRepository lojaRepository,
             IConfiguracaoSistemaRepository configRepository,
             ITokenProtector tokenProtector,
             IOAuthStateProtector stateProtector,
             ITenantContext tenantContext,
             IIntegracaoMercadoLivreRepository integracaoRepo,
             IIntegracaoLogRepository logRepo,
             IVeiculoPublicacaoRepository publicacaoRepo)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _veiculoRepository = veiculoRepository;
            _imagemRepository = imagemRepository;
            _lojaRepository = lojaRepository;
            _configRepository = configRepository;
             _tokenProtector = tokenProtector;
             _stateProtector = stateProtector;
             _tenantContext = tenantContext;
             _integracaoRepo = integracaoRepo;
             _logRepo = logRepo;
             _publicacaoRepo = publicacaoRepo;
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

         // Append-only audit log. Falhas de log NUNCA quebram operacao principal —
         // catch interno + log de fallback no ILogger. Codigos sao kebab-case
         // namespaceados por dominio: "oauth.callback.sucesso", "item.publicar.erro" etc.
         private async Task LogAsync(NivelIntegracaoLog nivel, string codigo, string mensagem, object metadados = null)
         {
             try
             {
                 var entry = new IntegracaoLog
                 {
                     IlgNivel = nivel,
                     IlgCodigo = codigo,
                     IlgMensagem = mensagem,
                     IlgMetadadosJson = metadados != null ? JsonSerializer.Serialize(metadados) : null
                 };
                 await _logRepo.CreateAsync(entry);
             }
             catch (Exception ex)
             {
                 _logger.LogWarning(ex, "Falha ao gravar IntegracaoLog (codigo={Codigo})", codigo);
             }
         }

        public string GetAuthUrl()
        {
            if (string.IsNullOrWhiteSpace(_settings.AppId))
                throw new InvalidOperationException("Mercado Livre nao configurado: defina AppId em appsettings ou via env var ML_APP_ID.");
            if (string.IsNullOrWhiteSpace(_settings.RedirectUri))
                throw new InvalidOperationException("Mercado Livre nao configurado: defina RedirectUri em appsettings ou via env var ML_REDIRECT_URI.");

             var tenantSlug = _tenantContext.IsResolved ? _tenantContext.TenantSlug : string.Empty;
             var state = _stateProtector.Proteger(tenantSlug);

             // State protege contra CSRF (atacante nao consegue forjar callback porque
             // nao tem a chave do DataProtection) e contra cross-tenant (state carrega
             // o slug e o callback valida que confere com o tenant resolvido).
             // offline_access e OBRIGATORIO para receber refresh_token — sem ele o ML
             // so devolve access_token de 6h e nao da pra renovar automaticamente.
             return $"{AuthUrl}?response_type=code&client_id={_settings.AppId}" +
                    $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
                    $"&scope=offline_access+read+write" +
                    $"&state={Uri.EscapeDataString(state)}";
        }

        public async Task HandleCallbackAsync(string code, string? state)
        {
             var tenantSlug = _tenantContext.IsResolved ? _tenantContext.TenantSlug : string.Empty;
             // Lanca OAuthStateException em qualquer falha (vazio, adulterado, expirado,
             // tenant errado). Controller traduz pra mensagem amigavel na pagina de callback.
             _stateProtector.Validar(state, tenantSlug);

            var request = new
            {
                grant_type = "authorization_code",
                client_id = _settings.AppId,
                client_secret = _settings.ClientSecret,
                code = code,
                redirect_uri = _settings.RedirectUri
            };

            var response = await _httpClient.PostAsJsonAsync("/oauth/token", request);

            // EnsureSuccessStatusCode lanca HttpRequestException com mensagem
            // generica. Lendo o body antes da pra retornar erro descritivo
            // (ex: invalid_grant, redirect_uri mismatch, etc) que aparece no
            // popup de callback.
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                 await LogAsync(NivelIntegracaoLog.Error, "oauth.callback.erro",
                     $"Troca de code por token rejeitada (HTTP {(int)response.StatusCode})",
                     new { status = (int)response.StatusCode, body = errorBody });
                throw new InvalidOperationException(
                    $"Mercado Livre rejeitou a troca de code por token (HTTP {(int)response.StatusCode}): {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            // access_token e sempre obrigatorio — sem ele nada funciona.
            if (!result.TryGetProperty("access_token", out var accessTokenEl))
                throw new InvalidOperationException("Resposta do Mercado Livre nao contem access_token.");

             // refresh_token vem quando a URL de autorizacao tem scope=offline_access.
             // Se NAO vier (app sem offline_access habilitado), salvamos so o access
             // token — integracao funciona pelas proximas 6h. Logamos warning pra ser
             // visivel no painel; usuario precisa reconectar quando expirar.
            _settings.AccessToken = accessTokenEl.GetString();
             _settings.RefreshToken = result.TryGetProperty("refresh_token", out var refreshTokenEl)
                 ? refreshTokenEl.GetString()
                 : null;
             if (string.IsNullOrEmpty(_settings.RefreshToken))
             {
                 await LogAsync(NivelIntegracaoLog.Warning, "oauth.callback.sem-refresh-token",
                     "ML nao retornou refresh_token — token expira em ~6h e precisa de reconexao manual. "
                     + "Verifique se o app ML tem 'offline_access' habilitado.");
                 _logger.LogWarning("ML callback sem refresh_token — app provavelmente sem offline_access.");
             }

            // user_id e opcional — algumas versoes da API ML nao retornam aqui
            // (precisa chamar /users/me separadamente). Antes, GetProperty
            // lancava KeyNotFoundException com "The given key was not present
            // in the dictionary." e o popup mostrava "Falha na conexao"
            // mesmo com o token ja salvo em memoria.
            if (result.TryGetProperty("user_id", out var userIdEl))
            {
                _settings.UserId = userIdEl.GetRawText();
            }

             // Persistir tokens cifrados (DataProtection). Em rotacao de chave,
             // tokens antigos ficam ilegiveis e usuario precisa reconectar — preco
             // aceitavel pra ter os tokens sempre cifrados no banco do tenant.
             int? expiresIn = result.TryGetProperty("expires_in", out var expEl) && expEl.TryGetInt32(out var expSec)
                 ? expSec
                 : (int?)null;
             await SaveTokensAsync(_settings.AccessToken!, _settings.RefreshToken!, expiresIn);
            if (!string.IsNullOrEmpty(_settings.UserId))
            {
                await _configRepository.SetValorAsync("ML_USER_ID", _settings.UserId);
                 var i = await _integracaoRepo.GetSingletonAsync();
                 if (i != null) { i.IntSellerId = _settings.UserId; await _integracaoRepo.UpdateAsync(i); }
            }

             await LogAsync(NivelIntegracaoLog.Info, "oauth.callback.sucesso",
                 "Integracao Mercado Livre autenticada com sucesso.",
                 new { sellerId = _settings.UserId, expiresIn });
            _logger.LogInformation("Mercado Livre conectado. UserId: {UserId}", _settings.UserId ?? "(nao retornado no token endpoint)");
        }

         private async Task SaveTokensAsync(string accessToken, string refreshToken, int? expiresInSec = null)
         {
             var integracao = await _integracaoRepo.EnsureSingletonAsync();
             integracao.IntAccessTokenCifrado = _tokenProtector.Protect(accessToken);
             // refresh_token pode ser null se o app nao tem offline_access.
             integracao.IntRefreshTokenCifrado = string.IsNullOrEmpty(refreshToken)
                 ? null
                 : _tokenProtector.Protect(refreshToken);
             if (expiresInSec.HasValue)
             {
                 var expiraEm = DateTime.UtcNow.AddSeconds(expiresInSec.Value);
                 _settings.AccessTokenExpiraEm = expiraEm;
                 integracao.IntAccessTokenExpiraEm = expiraEm;
             }
             integracao.IntStatus = StatusIntegracao.Ativa;
             integracao.IntMotivoErro = MotivoIntegracaoErro.Nenhum;
             integracao.IntFalhasConsecutivasSync = 0;
             if (integracao.IntAutenticadaEm is null)
                 integracao.IntAutenticadaEm = DateTime.UtcNow;
             await _integracaoRepo.UpdateAsync(integracao);
         }

         // Garante que ha um access token valido pelos proximos RefreshSkew segundos.
         // Refresha proativamente se estiver perto de expirar. Lock por tenant + double-check
         // evita corrida com refresh do ML (que invalida o token anterior).
         private async Task EnsureFreshTokenAsync()
         {
             await LoadTokensFromDbIfNeeded();

             if (string.IsNullOrEmpty(_settings.AccessToken) || string.IsNullOrEmpty(_settings.RefreshToken))
                 return; // nao conectado — chamadores devem checar IsConnectedAsync

             // Sem informacao de expiracao (cenario legado), assume valido pra preservar
             // comportamento atual. Proximo refresh (reativo via 401) preenchera o campo.
             if (_settings.AccessTokenExpiraEm is null)
                 return;

             if (DateTime.UtcNow < _settings.AccessTokenExpiraEm.Value - RefreshSkew)
                 return; // ainda dentro da janela segura

             var lockKey = _tenantContext.IsResolved ? _tenantContext.TenantSlug : "default";
             var sem = RefreshLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
             await sem.WaitAsync();
             try
             {
                 // Double-check: outra thread pode ter refresheado enquanto esperavamos o lock.
                 // Re-le do banco e checa a janela de novo antes de gastar uma chamada extra.
                 _settings.AccessToken = null; // invalida cache em memoria
                 await LoadTokensFromDbIfNeeded();
                 if (_settings.AccessTokenExpiraEm.HasValue
                     && DateTime.UtcNow < _settings.AccessTokenExpiraEm.Value - RefreshSkew)
                     return;

                 await RefreshTokenAsync();
             }
             finally
             {
                 sem.Release();
             }
         }

         // Decifra silenciosamente. Se o token foi gravado antes da Fase 1 (plain text)
         // ou cifrado com chave diferente (rotacao), retorna o valor como esta para nao
         // quebrar usuarios existentes; proxima reconexao ja salva cifrado.
         private string? TryUnprotect(string? value)
         {
             if (string.IsNullOrEmpty(value)) return value;
             try { return _tokenProtector.Unprotect(value); }
             catch (CryptographicException)
             {
                 _logger.LogWarning("Token ML em formato legado/incompativel — usando como esta. Reconecte para cifrar.");
                 return value;
             }
         }

        public async Task<bool> IsConnectedAsync()
        {
             await EnsureFreshTokenAsync();

            if (string.IsNullOrEmpty(_settings.AccessToken)) return false;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                     // Fallback reativo: skew nao previu (clock drift extremo ou token
                     // revogado server-side). Tenta refresh sob lock e considera conectado.
                     await EnsureFreshTokenAsync_Force();
                    return !string.IsNullOrEmpty(_settings.AccessToken);
                }

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<MercadoLivreContaInfo?> GetContaInfoAsync()
        {
             await EnsureFreshTokenAsync();
            if (string.IsNullOrEmpty(_settings.AccessToken)) return null;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                     await EnsureFreshTokenAsync_Force();
                    if (string.IsNullOrEmpty(_settings.AccessToken)) return null;
                    request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                    response = await _httpClient.SendAsync(request);
                }

                if (!response.IsSuccessStatusCode) return null;

                var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                return new MercadoLivreContaInfo
                {
                    Nickname = data.TryGetProperty("nickname", out var n) ? n.GetString() ?? "" : "",
                    Email = data.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "",
                    UserId = data.TryGetProperty("id", out var i) ? i.ToString() : (_settings.UserId ?? ""),
                    Pais = data.TryGetProperty("country_id", out var c) ? c.GetString() : null,
                    UrlPerfil = data.TryGetProperty("permalink", out var p) ? p.GetString() : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar info da conta ML");
                return null;
            }
        }

        public async Task DesconectarAsync()
        {
             // Limpa chaves legadas (compat) + entidade nova (fonte da verdade).
            await _configRepository.SetValorAsync("ML_ACCESS_TOKEN", "");
            await _configRepository.SetValorAsync("ML_REFRESH_TOKEN", "");
            await _configRepository.SetValorAsync("ML_USER_ID", "");
             await _configRepository.SetValorAsync("ML_ACCESS_TOKEN_EXPIRA_EM", "");

             var integracao = await _integracaoRepo.GetSingletonAsync();
             if (integracao != null)
             {
                 integracao.IntAccessTokenCifrado = null;
                 integracao.IntRefreshTokenCifrado = null;
                 integracao.IntAccessTokenExpiraEm = null;
                 integracao.IntSellerId = null;
                 integracao.IntMlNickname = null;
                 integracao.IntMlEmail = null;
                 integracao.IntStatus = StatusIntegracao.Inativa;
                 integracao.IntMotivoErro = MotivoIntegracaoErro.Nenhum;
                 await _integracaoRepo.UpdateAsync(integracao);
             }

            _settings.AccessToken = null;
            _settings.RefreshToken = null;
            _settings.UserId = null;
             _settings.AccessTokenExpiraEm = null;
             await LogAsync(NivelIntegracaoLog.Info, "oauth.desconectar", "Integracao Mercado Livre desconectada pelo usuario.");
            _logger.LogInformation("Mercado Livre desconectado.");
        }

        private async Task LoadTokensFromDbIfNeeded()
        {
            if (!string.IsNullOrEmpty(_settings.AccessToken)) return;

             // Prioridade: entidade nova (IntegracaoMercadoLivre). Fallback: chaves
             // antigas em Configuracoes (migracao lazy — primeira leitura copia pra
             // entidade e os campos antigos ficam orfaos ate desconexao/reconexao).
             var integracao = await _integracaoRepo.GetSingletonAsync();
             if (integracao != null && !string.IsNullOrEmpty(integracao.IntAccessTokenCifrado))
             {
                 _settings.AccessToken = TryUnprotect(integracao.IntAccessTokenCifrado);
                 _settings.RefreshToken = TryUnprotect(integracao.IntRefreshTokenCifrado);
                 _settings.UserId = integracao.IntSellerId;
                 _settings.AccessTokenExpiraEm = integracao.IntAccessTokenExpiraEm;
                 return;
             }

             // Fallback legado: le das chaves antigas. Se tem token la, migra pra nova entidade.
             var accessLegado = TryUnprotect(await _configRepository.GetValorAsync("ML_ACCESS_TOKEN"));
             var refreshLegado = TryUnprotect(await _configRepository.GetValorAsync("ML_REFRESH_TOKEN"));
             _settings.UserId = await _configRepository.GetValorAsync("ML_USER_ID");

             var expiraStr = await _configRepository.GetValorAsync("ML_ACCESS_TOKEN_EXPIRA_EM");
             if (!string.IsNullOrWhiteSpace(expiraStr)
                 && DateTime.TryParse(expiraStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expira))
             {
                 _settings.AccessTokenExpiraEm = expira;
             }

             _settings.AccessToken = accessLegado;
             _settings.RefreshToken = refreshLegado;

             // Migracao one-time: copia pra nova entidade e zera as chaves antigas.
             if (!string.IsNullOrEmpty(accessLegado) && !string.IsNullOrEmpty(refreshLegado))
             {
                 try
                 {
                     var novo = await _integracaoRepo.EnsureSingletonAsync();
                     novo.IntAccessTokenCifrado = _tokenProtector.Protect(accessLegado);
                     novo.IntRefreshTokenCifrado = _tokenProtector.Protect(refreshLegado);
                     novo.IntAccessTokenExpiraEm = _settings.AccessTokenExpiraEm;
                     novo.IntSellerId = _settings.UserId;
                     novo.IntStatus = StatusIntegracao.Ativa;
                     novo.IntMotivoErro = MotivoIntegracaoErro.Nenhum;
                     if (novo.IntAutenticadaEm is null) novo.IntAutenticadaEm = DateTime.UtcNow;
                     await _integracaoRepo.UpdateAsync(novo);
                     // Limpa chaves antigas pra nao reler.
                     await _configRepository.SetValorAsync("ML_ACCESS_TOKEN", "");
                     await _configRepository.SetValorAsync("ML_REFRESH_TOKEN", "");
                     await _configRepository.SetValorAsync("ML_ACCESS_TOKEN_EXPIRA_EM", "");
                     await LogAsync(NivelIntegracaoLog.Info, "ml.migracao.legado-para-entidade",
                         "Tokens ML migrados de Configuracoes para IntegracaoMercadoLivre.");
                 }
                 catch (Exception ex)
                 {
                     _logger.LogWarning(ex, "Falha ao migrar tokens ML para a entidade nova — continua usando do legado.");
                 }
             }
        }

        public async Task<(string ExternoId, string Url)> PublicarVeiculoAsync(int veiculoId)
        {
            await EnsureTokenAsync();

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) throw new Exception("Veículo não encontrado.");

             // ML rejeita "0 km" pra user comum (precisa ser concessionaria). Valida
             // antes da chamada pra dar erro claro ao operador.
             if (veiculo.VeiKm <= 0)
                 throw new Exception("Veiculo com 0 km nao pode ser anunciado por usuario comum no Mercado Livre. Atualize a quilometragem antes.");

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var urlBase = loja?.LojUrlCatalogo?.TrimEnd('/') ?? "";

            var pictures = imagens
                .Where(i => i.ImgSts)
                .OrderBy(i => i.ImgOrdem)
                .Select(i => new { source = $"{urlBase}/api/imagens/file?path={Uri.EscapeDataString(i.ImgCaminho)}" })
                .ToList();

            var item = new
            {
                title = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}",
                category_id = "MLB1744", // Carros, Vans e Utilitarios
                price = veiculo.VeiPreco,
                currency_id = "BRL",
                available_quantity = 1,
                 // Categoria de veiculos no ML BR (MLB1744) so aceita anuncio
                 // CLASSIFICADO — comprador entra em contato com vendedor, sem
                 // checkout direto. ML retorna "Category only supports listing
                 // modes: [classified]" se enviarmos buy_it_now/gold_special.
                 buying_mode = "classified",
                condition = "used",
                 listing_type_id = "gold_premium", // Anuncio classificado premium (maior visibilidade)
                description = new { plain_text = MontarDescricao(veiculo, loja) },
                pictures = pictures,
                 // ML exige location ate o nivel de cidade pra anuncio de veiculo.
                 // Fallbacks generosos: se a loja nao tem cidade/estado cadastrado, usa
                 // valores padrao pra nao explodir a chamada (cliente corrige depois
                 // no painel do ML).
                 location = new
                 {
                     country = new { name = "Brasil" },
                     state = new { name = string.IsNullOrWhiteSpace(loja?.LojEstado) ? "São Paulo" : loja.LojEstado },
                     city = new { name = string.IsNullOrWhiteSpace(loja?.LojCidade) ? "São Paulo" : loja.LojCidade }
                 },
                attributes = new object[]
                {
                    new { id = "BRAND", value_name = veiculo.VeiMarca },
                    new { id = "MODEL", value_name = veiculo.VeiModelo },
                    new { id = "VEHICLE_YEAR", value_name = veiculo.VeiAno.ToString() },
                     // ML exige unidade no valor — "8000" sem unidade e' rejeitado.
                     new { id = "KILOMETERS", value_name = $"{veiculo.VeiKm} km" },
                    new { id = "COLOR", value_name = veiculo.VeiCor ?? "" },
                     // Atributos OBRIGATORIOS pra MLB1744 + channel marketplace. Sem
                     // campos proprios no veiculo, mandamos defaults: 4 portas e
                     // "Gasolina e álcool" (Flex). ML normaliza pro value_id correto.
                     new { id = "DOORS", value_name = "4" },
                     new { id = "FUEL_TYPE", value_name = "Gasolina e álcool" },
                     // TRIM (versao) — sem campo dedicado, repete o modelo como aproximacao.
                     new { id = "TRIM", value_name = veiculo.VeiModelo },
                    new { id = "ITEM_CONDITION", value_name = "Usado" }
                }
            };

            var json = JsonSerializer.Serialize(item);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/items");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao publicar no ML: {Response}", responseBody);
                 await LogAsync(NivelIntegracaoLog.Error, "item.publicar.erro",
                     $"Falha ao publicar veiculo {veiculoId} (HTTP {(int)response.StatusCode}).",
                     new { veiculoId, status = (int)response.StatusCode, body = responseBody });
                throw new Exception($"Erro ao publicar no Mercado Livre: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var externoId = result.GetProperty("id").GetString();
            var permalink = result.GetProperty("permalink").GetString();

             await LogAsync(NivelIntegracaoLog.Info, "item.publicar.sucesso",
                 $"Veiculo {veiculoId} publicado no ML como {externoId}.",
                 new { veiculoId, externoId, permalink });
            _logger.LogInformation("Veiculo {VeiculoId} publicado no ML: {ExternoId}", veiculoId, externoId);

            return (externoId, permalink);
        }

        public async Task RemoverAnuncioAsync(string externoId)
        {
            await EnsureTokenAsync();

            var json = JsonSerializer.Serialize(new { status = "closed" });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{externoId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro ao remover anuncio ML {ExternoId}: {Response}", externoId, responseBody);
            }
            else
            {
                _logger.LogInformation("Anuncio ML {ExternoId} removido", externoId);
            }
        }

         // Topic "items" e o mais comum: ML notifica mudancas de status (active,
         // paused, closed). Buscamos o item via API e atualizamos a publicacao local.
         // Outros topics (orders_v2, questions, payments) sao apenas logados — implementar
         // handlers dedicados depois conforme a necessidade.
         public async Task ProcessarNotificacaoAsync(string topic, string resource)
         {
             if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(resource))
             {
                 await LogAsync(NivelIntegracaoLog.Warning, "webhook.payload-invalido",
                     "Notificacao ML sem topic ou resource.", new { topic, resource });
                 return;
             }

             if (topic != "items")
             {
                 await LogAsync(NivelIntegracaoLog.Info, "webhook.topic-nao-tratado",
                     $"Topic '{topic}' ainda sem handler dedicado — apenas logado.",
                     new { topic, resource });
                 return;
             }

             // resource vem como "/items/MLBxxxxxxxxx" — extrai o ID externo.
             var externoId = resource.StartsWith("/items/", StringComparison.OrdinalIgnoreCase)
                 ? resource.Substring("/items/".Length)
                 : resource;

             var publicacao = await _publicacaoRepo.GetAtivaByExternoIdAsync(externoId, "MercadoLivre");
             if (publicacao == null)
             {
                 // Anuncio nao bate com nada nosso — ja removido ou criado fora do sistema.
                 await LogAsync(NivelIntegracaoLog.Info, "webhook.publicacao-nao-encontrada",
                     $"Notificacao ML recebida para item {externoId} sem publicacao local correspondente.",
                     new { externoId });
                 return;
             }

             // GET no item pra ver status atual. EnsureToken garante refresh proativo.
             try
             {
                 await EnsureTokenAsync();
                 var req = new HttpRequestMessage(HttpMethod.Get, $"/items/{externoId}?attributes=id,status,permalink");
                 req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                 var resp = await _httpClient.SendAsync(req);
                 var body = await resp.Content.ReadAsStringAsync();

                 if (!resp.IsSuccessStatusCode)
                 {
                     await LogAsync(NivelIntegracaoLog.Warning, "webhook.fetch-item-erro",
                         $"GET /items/{externoId} falhou (HTTP {(int)resp.StatusCode}).",
                         new { externoId, status = (int)resp.StatusCode, body });
                     return;
                 }

                 var data = JsonSerializer.Deserialize<JsonElement>(body);
                 var status = data.TryGetProperty("status", out var s) ? s.GetString() : null;

                 // ML status -> nosso ciclo. "active"/"paused" continuam ATIVO localmente
                 // (so registra log); "closed" significa anuncio encerrado (vendido,
                 // cancelado pelo usuario, removido pelo ML) — marca REMOVIDO local.
                 if (string.Equals(status, "closed", StringComparison.OrdinalIgnoreCase))
                 {
                     publicacao.Remover();
                     await _publicacaoRepo.UpdateAsync(publicacao);
                     await LogAsync(NivelIntegracaoLog.Info, "webhook.publicacao-fechada",
                         $"Anuncio {externoId} marcado como closed no ML — publicacao local marcada REMOVIDO.",
                         new { externoId, veiculoId = publicacao.R_VeiId });
                 }
                 else
                 {
                     await LogAsync(NivelIntegracaoLog.Info, "webhook.item-status",
                         $"Notificacao processada — status atual: {status}.",
                         new { externoId, status, veiculoId = publicacao.R_VeiId });
                 }
             }
             catch (Exception ex)
             {
                 await LogAsync(NivelIntegracaoLog.Error, "webhook.excecao", ex.Message,
                     new { externoId, exception = ex.GetType().Name });
                 _logger.LogError(ex, "Erro processando webhook ML para item {ExternoId}", externoId);
             }
         }

        public async Task AtualizarAnuncioAsync(string externoId, int veiculoId)
        {
            await EnsureTokenAsync();

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return;

            var json = JsonSerializer.Serialize(new
            {
                price = veiculo.VeiPreco,
                title = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}"
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{externoId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
            request.Content = content;

            await _httpClient.SendAsync(request);
        }

        private async Task RefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(_settings.RefreshToken)) return;

            try
            {
                var request = new
                {
                    grant_type = "refresh_token",
                    client_id = _settings.AppId,
                    client_secret = _settings.ClientSecret,
                    refresh_token = _settings.RefreshToken
                };

                var response = await _httpClient.PostAsJsonAsync("/oauth/token", request);
                 if (!response.IsSuccessStatusCode)
                 {
                     var body = await response.Content.ReadAsStringAsync();
                     // invalid_grant = refresh_token nao serve mais (revogado, expirado ou
                     // ja consumido por outra thread). Marca a integracao ComErro pra UI
                     // pedir reconexao. Outros 4xx/5xx sao transientes — nao mudam status.
                     var isInvalidGrant = body?.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) == true;
                     if (isInvalidGrant)
                     {
                         await MarcarComErroAsync(MotivoIntegracaoErro.Autenticacao,
                             $"refresh_token rejeitado pelo ML ({(int)response.StatusCode})");
                         await LogAsync(NivelIntegracaoLog.Error, "oauth.refresh.invalid-grant",
                             "Refresh token invalido — usuario precisa reconectar.",
                             new { status = (int)response.StatusCode, body });
                         _settings.AccessToken = null;
                         return;
                     }
                     await LogAsync(NivelIntegracaoLog.Warning, "oauth.refresh.erro-transiente",
                         $"Refresh ML falhou (HTTP {(int)response.StatusCode}) — tentara de novo.",
                         new { status = (int)response.StatusCode, body });
                     response.EnsureSuccessStatusCode(); // joga pro catch generico
                     return;
                 }

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                _settings.AccessToken = result.GetProperty("access_token").GetString();
                _settings.RefreshToken = result.GetProperty("refresh_token").GetString();

                 int? expiresIn = result.TryGetProperty("expires_in", out var expEl) && expEl.TryGetInt32(out var expSec)
                     ? expSec
                     : (int?)null;
                 await SaveTokensAsync(_settings.AccessToken!, _settings.RefreshToken!, expiresIn);

                 await LogAsync(NivelIntegracaoLog.Info, "oauth.refresh.sucesso",
                     $"Access token renovado (expira em {expiresIn?.ToString() ?? "?"}s).");
                _logger.LogInformation("Token do Mercado Livre renovado com sucesso (expira em {ExpiresIn}s)", expiresIn?.ToString() ?? "?");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao renovar token do Mercado Livre");
                 await LogAsync(NivelIntegracaoLog.Error, "oauth.refresh.excecao", ex.Message);
                _settings.AccessToken = null;
            }
        }

         private async Task MarcarComErroAsync(MotivoIntegracaoErro motivo, string mensagemLog)
         {
             try
             {
                 var i = await _integracaoRepo.GetSingletonAsync();
                 if (i == null) return;
                 i.IntStatus = StatusIntegracao.ComErro;
                 i.IntMotivoErro = motivo;
                 i.IntFalhasConsecutivasSync++;
                 await _integracaoRepo.UpdateAsync(i);
             }
             catch (Exception ex)
             {
                 _logger.LogWarning(ex, "Falha ao marcar integracao ML ComErro ({Motivo}): {Msg}", motivo, mensagemLog);
             }
         }

        private async Task EnsureTokenAsync()
        {
             await EnsureFreshTokenAsync();
            if (string.IsNullOrEmpty(_settings.AccessToken))
                throw new Exception("Mercado Livre nao esta conectado. Configure a integracao primeiro.");
        }

         // Variante "forçada" do refresh: usada como fallback quando o ML retorna
         // 401 mesmo dentro da janela teorica de validade (clock drift extremo,
         // token revogado server-side via Apps/Permissions). Usa o mesmo lock pra
         // evitar dois refreshes em paralelo.
         private async Task EnsureFreshTokenAsync_Force()
         {
             if (string.IsNullOrEmpty(_settings.RefreshToken)) return;
             var lockKey = _tenantContext.IsResolved ? _tenantContext.TenantSlug : "default";
             var sem = RefreshLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
             await sem.WaitAsync();
             try { await RefreshTokenAsync(); }
             finally { sem.Release(); }
         }

        private static string MontarDescricao(Veiculo veiculo, Loja loja)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}");
            sb.AppendLine();
            if (veiculo.VeiKm > 0) sb.AppendLine($"Quilometragem: {veiculo.VeiKm:N0} km");
            if (!string.IsNullOrEmpty(veiculo.VeiCor)) sb.AppendLine($"Cor: {veiculo.VeiCor}");
            if (!string.IsNullOrEmpty(veiculo.VeiOpcionais))
            {
                sb.AppendLine();
                sb.AppendLine("Opcionais:");
                foreach (var opc in veiculo.VeiOpcionais.Split(','))
                    sb.AppendLine($"- {opc.Trim()}");
            }
            if (!string.IsNullOrEmpty(veiculo.VeiObservacao))
            {
                sb.AppendLine();
                sb.AppendLine(veiculo.VeiObservacao);
            }
            if (loja != null)
            {
                sb.AppendLine();
                sb.AppendLine($"{loja.LojNome}");
                if (!string.IsNullOrEmpty(loja.LojCidade)) sb.AppendLine($"{loja.LojCidade}/{loja.LojEstado}");
                if (!string.IsNullOrEmpty(loja.LojWhatsApp)) sb.AppendLine($"WhatsApp: {loja.LojWhatsApp}");
            }
            return sb.ToString();
        }
    }
}
