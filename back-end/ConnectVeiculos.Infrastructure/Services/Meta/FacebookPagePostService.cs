using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Catalogo;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Meta
{
    public class FacebookPagePostService : IFacebookPagePostService
    {
        public const string KEY_AUTO_POST = "META_FB_AUTO_POST";

        private readonly HttpClient _httpClient;
        private readonly MetaSettings _settings;
        private readonly IConfiguracaoSistemaRepository _configRepository;
        private readonly ITokenProtector _tokenProtector;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly ILogger<FacebookPagePostService> _logger;

        private readonly ITenantContext _tenantContext;
        private readonly IRedesPersonalizacaoService? _personalizacao;

        public FacebookPagePostService(
            HttpClient httpClient,
            IOptions<MetaSettings> settings,
            IConfiguracaoSistemaRepository configRepository,
            ITokenProtector tokenProtector,
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            ILojaRepository lojaRepository,
            ILogger<FacebookPagePostService> logger,
            ITenantContext tenantContext,
            IRedesPersonalizacaoService? personalizacao = null)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _configRepository = configRepository;
            _tokenProtector = tokenProtector;
            _veiculoRepository = veiculoRepository;
            _imagemRepository = imagemRepository;
            _lojaRepository = lojaRepository;
            _logger = logger;
            _tenantContext = tenantContext;
            _personalizacao = personalizacao;
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var (token, pageId) = await ResolvePageCredentialsAsync();
            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(pageId);
        }

        public async Task<FacebookPagePostConfigInfo> GetConfigAsync()
        {
            var (token, pageId) = await ResolvePageCredentialsAsync();
            var pageNome = await _configRepository.GetValorAsync(MetaOAuthService.KEY_PAGE_NOME);
            return new FacebookPagePostConfigInfo
            {
                PageConectada = !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(pageId),
                PageId = string.IsNullOrEmpty(pageId) ? null : pageId,
                PageNome = string.IsNullOrEmpty(pageNome) ? null : pageNome,
                AutoPostHabilitado = await AutoPostHabilitadoAsync()
            };
        }

        public async Task SetAutoPostHabilitadoAsync(bool habilitado)
        {
            await _configRepository.SetValorAsync(KEY_AUTO_POST, habilitado ? "true" : "false");
            _logger.LogInformation("Facebook auto-post {Status} pelo operador.", habilitado ? "HABILITADO" : "DESABILITADO");
        }

        public async Task<TestIntegracaoResult> TestarAsync()
        {
            var (token, pageId) = await ResolvePageCredentialsAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(pageId))
                return new TestIntegracaoResult { Sucesso = false, Mensagem = "Page nao selecionada. Conecte sua conta Meta primeiro." };

            try
            {
                var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}?fields=name,fan_count&access_token={Uri.EscapeDataString(token)}";
                var resp = await _httpClient.GetAsync(url);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return new TestIntegracaoResult { Sucesso = false, Mensagem = MetaErro.Descrever((int)resp.StatusCode, body) };

                using var doc = JsonDocument.Parse(body);
                var nome = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : "";
                var fans = doc.RootElement.TryGetProperty("fan_count", out var f) ? f.GetInt32() : 0;
                return new TestIntegracaoResult { Sucesso = true, Mensagem = $"Page '{nome}' OK. {fans} seguidor(es)." };
            }
            catch (Exception ex)
            {
                return new TestIntegracaoResult { Sucesso = false, Mensagem = ex.Message };
            }
        }

        public const string PLATAFORMA = "FacebookPage";

        public async Task<PublicacaoResult?> PublicarVeiculoAsync(int veiculoId)
        {
            if (!await AutoPostHabilitadoAsync())
            {
                _logger.LogDebug("Facebook auto-post desabilitado, pulando veiculo {VeiculoId}", veiculoId);
                return null;
            }
            return await PublicarInternoAsync(veiculoId, null);
        }

        public async Task<PublicacaoResult?> PublicarManualAsync(int veiculoId, string? modeloLegenda = null)
            => await PublicarInternoAsync(veiculoId, modeloLegenda);

        private async Task<PublicacaoResult?> PublicarInternoAsync(int veiculoId, string? modeloLegenda)
        {
            var (token, pageId) = await ResolvePageCredentialsAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(pageId))
            {
                _logger.LogDebug("Facebook Page nao configurada, pulando veiculo {VeiculoId}", veiculoId);
                return null;
            }

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return null;

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? NormalizeBaseUrl(_settings?.PublicSiteUrl);
            if (baseUrl == null)
            {
                _logger.LogWarning("Facebook Page post abortado: sem URL publica configurada (veiculo {VeiculoId})", veiculoId);
                return null;
            }

            var slug = _tenantContext.IsResolved ? _tenantContext.TenantSlug : null;
            var linkVeiculo = CatalogoUrl.Veiculo(baseUrl, slug, veiculoId);
            // A primeira imagem e' a capa: a arte que a loja enviou ou a nossa
            // arte padrao com foto, preco e loja. Sem ela, so as fotos.
            var capa = _personalizacao == null ? null : await _personalizacao.CapaDoPostAsync(veiculoId, baseUrl);
            var fotos = imagens.Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem)
                .Take(capa == null ? MaxFotosPorPost : MaxFotosPorPost - 1)
                .Select(i => $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(i.ImgCaminho)}&max=1440&format=jpeg")
                .ToList();
            if (capa != null) fotos.Insert(0, capa);

            var legenda = LegendaPost.Montar(modeloLegenda ?? await ModeloDaLojaAsync(), LegendaPost.Facebook, veiculo, loja, linkVeiculo);

            try
            {
                string? postId = null;
                if (fotos.Count > 1)
                {
                    postId = await PublicarVariasFotosAsync(pageId, token, fotos, legenda, veiculoId);
                    if (postId == null)
                        _logger.LogWarning("Facebook Page post veiculo {VeiculoId}: post com varias fotos falhou, tentando so a principal", veiculoId);
                }
                postId ??= await PublicarUmaFotoAsync(pageId, token, fotos.FirstOrDefault() ?? "", legenda, veiculoId);
                if (string.IsNullOrEmpty(postId)) return null;

                _logger.LogInformation("Facebook Page post veiculo {VeiculoId}: OK", veiculoId);
                return new PublicacaoResult
                {
                    ExternoId = postId,
                    Url = $"https://www.facebook.com/{postId}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao postar veiculo {VeiculoId} na Facebook Page", veiculoId);
                return null;
            }
        }

        // O Facebook aceita mais, mas o post mostra no maximo 5 na grade e
        // 10 ja cobre o carro; mesmo limite do carrossel do Instagram.
        private const int MaxFotosPorPost = 10;

        /// <summary>Post de uma foto: POST /{page-id}/photos com url e message (vai direto pro feed).</summary>
        private async Task<string?> PublicarUmaFotoAsync(string pageId, string token, string imageUrl, string legenda, int veiculoId)
        {
            var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}/photos";
            var payload = new { url = imageUrl, message = legenda, access_token = token };
            var resp = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook Page post veiculo {VeiculoId} falhou: {Body}", veiculoId, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            // Resposta: { "id": "{photo-id}", "post_id": "{page-id}_{post-id}" }
            return doc.RootElement.TryGetProperty("post_id", out var pid) ? pid.GetString()
                 : doc.RootElement.TryGetProperty("id", out var id) ? id.GetString()
                 : null;
        }

        /// <summary>
        /// Post com varias fotos: cada foto sobe sem ir pro feed (published=false)
        /// e o post em /{page-id}/feed junta todas em attached_media. Se menos de
        /// 2 fotos subirem, devolve null e quem chamou cai no post de uma foto.
        /// </summary>
        private async Task<string?> PublicarVariasFotosAsync(string pageId, string token, List<string> fotos, string legenda, int veiculoId)
        {
            var fotoUrl = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}/photos";
            var ids = new List<string>();
            // Uma de cada vez: o Facebook baixa cada foto do nosso servidor.
            foreach (var foto in fotos)
            {
                var payload = new { url = foto, published = false, access_token = token };
                var resp = await _httpClient.PostAsync(fotoUrl, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Facebook Page post veiculo {VeiculoId}: foto {Foto} nao subiu: {Body}", veiculoId, foto, body);
                    continue;
                }
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } photoId)
                    ids.Add(photoId);
            }
            if (ids.Count < 2) return null;

            var feedUrl = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}/feed";
            var post = new
            {
                message = legenda,
                attached_media = ids.Select(i => new { media_fbid = i }).ToArray(),
                access_token = token
            };
            var postResp = await _httpClient.PostAsync(feedUrl, new StringContent(JsonSerializer.Serialize(post), Encoding.UTF8, "application/json"));
            var postBody = await postResp.Content.ReadAsStringAsync();
            if (!postResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook Page post veiculo {VeiculoId} com {Qtd} fotos falhou: {Body}", veiculoId, ids.Count, postBody);
                return null;
            }

            using var postDoc = JsonDocument.Parse(postBody);
            // Resposta: { "id": "{page-id}_{post-id}" }
            return postDoc.RootElement.TryGetProperty("id", out var postId) ? postId.GetString() : null;
        }

        public async Task<bool> AtualizarLegendaDoPostAsync(string postId, int veiculoId, string statusVeiculo, string? modeloLegenda = null)
        {
            if (string.IsNullOrWhiteSpace(postId)) return false;

            var (token, pageId) = await ResolvePageCredentialsAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(pageId))
            {
                _logger.LogDebug("Facebook Page nao configurado; post do veiculo {VeiculoId} fica como esta.", veiculoId);
                return false;
            }

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return false;

            var selo = SeloDoStatus(statusVeiculo);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? NormalizeBaseUrl(_settings?.PublicSiteUrl);
            var slug = _tenantContext.IsResolved ? _tenantContext.TenantSlug : null;
            var linkVeiculo = baseUrl == null ? "" : CatalogoUrl.Veiculo(baseUrl, slug, veiculoId);

            // Mantem a legenda original abaixo do selo: quem ja viu o post
            // reconhece o anuncio, e quem chega depois entende que acabou.
            var textoBase = LegendaPost.Montar(modeloLegenda ?? await ModeloDaLojaAsync(), LegendaPost.Facebook, veiculo, loja, linkVeiculo);
            var legenda = selo.Length == 0 ? textoBase : selo + "\n\n" + textoBase;

            try
            {
                var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{postId}";
                var payload = new { message = legenda, access_token = token };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var resp = await _httpClient.PostAsync(url, content);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Nao consegui atualizar a legenda do post {PostId} do veiculo {VeiculoId} (status {Status}): {Body}",
                        postId, veiculoId, statusVeiculo, await resp.Content.ReadAsStringAsync());
                    return false;
                }

                _logger.LogInformation(
                    "Legenda do post {PostId} do veiculo {VeiculoId} atualizada (status {Status}).",
                    postId, veiculoId, statusVeiculo);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar legenda do post {PostId} do veiculo {VeiculoId}", postId, veiculoId);
                return false;
            }
        }

        public const string PLATAFORMA_STORY = "FacebookStory";

        public async Task<PublicacaoResult?> PublicarStoryAsync(int veiculoId, string imageUrl)
        {
            var (token, pageId) = await ResolvePageCredentialsAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(pageId))
            {
                _logger.LogDebug("Facebook Page nao configurada, story do veiculo {VeiculoId} pulado", veiculoId);
                return null;
            }

            try
            {
                // Story de Page sao dois passos: a foto sobe sem ir pro feed
                // (published=false) e o id dela vira o story.
                var fotoUrl = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}/photos";
                var fotoPayload = new { url = imageUrl, published = false, access_token = token };
                var fotoResp = await _httpClient.PostAsync(fotoUrl,
                    new StringContent(JsonSerializer.Serialize(fotoPayload), Encoding.UTF8, "application/json"));
                var fotoBody = await fotoResp.Content.ReadAsStringAsync();
                if (!fotoResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Facebook story veiculo {VeiculoId}: upload da foto falhou: {Body}", veiculoId, fotoBody);
                    return null;
                }

                string? photoId;
                using (var doc = JsonDocument.Parse(fotoBody))
                    photoId = doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
                if (string.IsNullOrEmpty(photoId)) return null;

                var storyUrl = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}/photo_stories";
                var storyPayload = new { photo_id = photoId, access_token = token };
                var storyResp = await _httpClient.PostAsync(storyUrl,
                    new StringContent(JsonSerializer.Serialize(storyPayload), Encoding.UTF8, "application/json"));
                var storyBody = await storyResp.Content.ReadAsStringAsync();
                if (!storyResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Facebook story veiculo {VeiculoId} falhou: {Body}", veiculoId, storyBody);
                    return null;
                }

                string? postId;
                using (var doc = JsonDocument.Parse(storyBody))
                    postId = doc.RootElement.TryGetProperty("post_id", out var pid) ? pid.GetString() : photoId;

                _logger.LogInformation("Facebook story veiculo {VeiculoId}: OK", veiculoId);
                return new PublicacaoResult
                {
                    ExternoId = postId ?? photoId,
                    // Story nao tem endereco proprio estavel; a Page mostra o anel.
                    Url = $"https://www.facebook.com/{pageId}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar story do veiculo {VeiculoId} na Facebook Page", veiculoId);
                return null;
            }
        }

        /// <summary>
        /// Faixa que abre a legenda. Disponivel nao ganha nada: e o anuncio normal.
        /// </summary>
        private static string SeloDoStatus(string status) => status switch
        {
            "D" => "",
            "V" => "✅ VENDIDO",
            "R" => "🔒 RESERVADO",
            _ => "⛔ INDISPONÍVEL"
        };

        // Sem o servico (testes antigos) vale a legenda padrao.
        private async Task<string?> ModeloDaLojaAsync()
            => _personalizacao == null ? null : await _personalizacao.ModeloLegendaAsync(LegendaPost.Facebook);

        private async Task<bool> AutoPostHabilitadoAsync()
        {
            var v = await _configRepository.GetValorAsync(KEY_AUTO_POST);
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<(string token, string pageId)> ResolvePageCredentialsAsync()
        {
            var pageId = await _configRepository.GetValorAsync(MetaOAuthService.KEY_PAGE_ID) ?? "";
            var cifrado = await _configRepository.GetValorAsync(MetaOAuthService.KEY_PAGE_TOKEN_CIFRADO);
            if (string.IsNullOrEmpty(cifrado)) return ("", pageId);
            try { return (_tokenProtector.Unprotect(cifrado), pageId); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Meta Page token nao pode ser decifrado (chave rotacionada?)");
                return ("", pageId);
            }
        }

        private static string? NormalizeBaseUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                if (!Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out uri))
                    return null;
            }
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            if (string.IsNullOrWhiteSpace(uri.Host)) return null;
            var port = uri.IsDefaultPort ? "" : ":" + uri.Port;
            return $"{uri.Scheme}://{uri.Host}{port}";
        }
    }
}
