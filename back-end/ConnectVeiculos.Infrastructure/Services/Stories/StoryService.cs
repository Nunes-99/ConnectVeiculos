using System.Globalization;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.Meta;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace ConnectVeiculos.Infrastructure.Services.Stories
{
    /// <inheritdoc cref="IStoryService"/>
    public class StoryService : IStoryService
    {
        public const string KEY_NO_INSTAGRAM = "META_STORY_NO_INSTAGRAM";
        public const string KEY_NO_FACEBOOK = "META_STORY_NO_FACEBOOK";
        public const string KEY_AO_CHEGAR = "META_STORY_AO_CHEGAR";
        public const string KEY_AO_BAIXAR_PRECO = "META_STORY_AO_BAIXAR_PRECO";
        public const string KEY_AO_VENDER = "META_STORY_AO_VENDER";

        // A arte so precisa existir ate a Meta baixar. Dois dias de folga cobrem
        // uma nova tentativa manual sem deixar a pasta crescer para sempre.
        private static readonly TimeSpan ValidadeArte = TimeSpan.FromDays(2);

        // Preco que baixa varias vezes no mesmo dia viraria uma sequencia de
        // stories do mesmo carro. Um por dia ja avisa quem segue a loja.
        private const int HorasEntreStoriesDePreco = 20;

        private static readonly CultureInfo PtBr = new("pt-BR");

        private readonly IConfiguracaoSistemaRepository _configRepository;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly IVeiculoPublicacaoRepository _publicacaoRepository;
        private readonly IInstagramPostService _instagramPostService;
        private readonly IFacebookPagePostService _facebookPagePostService;
        private readonly MetaSettings _settings;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<StoryService> _logger;

        public StoryService(
            IConfiguracaoSistemaRepository configRepository,
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            ILojaRepository lojaRepository,
            IVeiculoPublicacaoRepository publicacaoRepository,
            IInstagramPostService instagramPostService,
            IFacebookPagePostService facebookPagePostService,
            IOptions<MetaSettings> settings,
            IHostEnvironment environment,
            ILogger<StoryService> logger)
        {
            _configRepository = configRepository;
            _veiculoRepository = veiculoRepository;
            _imagemRepository = imagemRepository;
            _lojaRepository = lojaRepository;
            _publicacaoRepository = publicacaoRepository;
            _instagramPostService = instagramPostService;
            _facebookPagePostService = facebookPagePostService;
            _settings = settings.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task<StoryConfigInfo> GetConfigAsync()
        {
            return new StoryConfigInfo
            {
                NoInstagram = await LigadoAsync(KEY_NO_INSTAGRAM),
                NoFacebook = await LigadoAsync(KEY_NO_FACEBOOK),
                AoChegar = await LigadoAsync(KEY_AO_CHEGAR),
                AoBaixarPreco = await LigadoAsync(KEY_AO_BAIXAR_PRECO),
                AoVender = await LigadoAsync(KEY_AO_VENDER),
                InstagramConectado = await _instagramPostService.IsConfiguredAsync(),
                FacebookConectado = await _facebookPagePostService.IsConfiguredAsync()
            };
        }

        public async Task SalvarConfigAsync(StoryConfigInput config)
        {
            await GravarAsync(KEY_NO_INSTAGRAM, config.NoInstagram);
            await GravarAsync(KEY_NO_FACEBOOK, config.NoFacebook);
            await GravarAsync(KEY_AO_CHEGAR, config.AoChegar);
            await GravarAsync(KEY_AO_BAIXAR_PRECO, config.AoBaixarPreco);
            await GravarAsync(KEY_AO_VENDER, config.AoVender);
            _logger.LogInformation(
                "Stories configurados: Instagram={Ig} Facebook={Fb} chegar={Chegar} preco={Preco} vender={Vender}",
                config.NoInstagram, config.NoFacebook, config.AoChegar, config.AoBaixarPreco, config.AoVender);
        }

        public async Task PublicarAutomaticoAsync(int veiculoId, string evento, decimal? precoAnterior = null)
        {
            try
            {
                var chave = evento switch
                {
                    EventoStory.Novo => KEY_AO_CHEGAR,
                    EventoStory.PrecoBaixou => KEY_AO_BAIXAR_PRECO,
                    EventoStory.Vendido => KEY_AO_VENDER,
                    _ => null
                };
                if (chave == null || !await LigadoAsync(chave)) return;

                if (evento == EventoStory.PrecoBaixou && await TeveStoryRecenteAsync(veiculoId))
                {
                    _logger.LogInformation(
                        "Story de preco do veiculo {VeiculoId} pulado: ja houve story dele nas ultimas {Horas}h.",
                        veiculoId, HorasEntreStoriesDePreco);
                    return;
                }

                var r = await PublicarAsync(veiculoId, evento, precoAnterior);
                if (!r.Sucesso)
                    _logger.LogInformation("Story {Evento} do veiculo {VeiculoId} nao publicado: {Motivo}", evento, veiculoId, r.Mensagem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar story {Evento} do veiculo {VeiculoId}", evento, veiculoId);
            }
        }

        public Task<StoryPublicacaoResultado> PublicarManualAsync(int veiculoId)
            => PublicarAsync(veiculoId, EventoStory.Manual, null);

        private async Task<StoryPublicacaoResultado> PublicarAsync(int veiculoId, string evento, decimal? precoAnterior)
        {
            var noInstagram = await LigadoAsync(KEY_NO_INSTAGRAM) && await _instagramPostService.IsConfiguredAsync();
            var noFacebook = await LigadoAsync(KEY_NO_FACEBOOK) && await _facebookPagePostService.IsConfiguredAsync();
            if (!noInstagram && !noFacebook)
                return Falha("Escolha onde publicar os stories em Integrações (Instagram e/ou Facebook conectados).");

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return Falha("Veículo não encontrado.");
            if (veiculo.VeiSts == "I") return Falha("Veículo inativo não ganha story.");

            var imagem = (await _imagemRepository.GetByVeiculoIdAsync(veiculoId))
                .Where(i => i.ImgSts)
                .OrderBy(i => i.ImgOrdem)
                .FirstOrDefault();
            if (imagem == null) return Falha("O veículo não tem foto.");

            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? NormalizeBaseUrl(_settings?.PublicSiteUrl);
            if (baseUrl == null || !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Falha("O endereço público do site precisa ser HTTPS para a Meta baixar a imagem.");

            var arquivoFoto = CaminhoNoDisco(imagem.ImgCaminho);
            if (!File.Exists(arquivoFoto)) return Falha("O arquivo da foto principal não existe no servidor.");

            // Manual de carro que ja saiu mostra o que aconteceu com ele, nao preco.
            if (evento == EventoStory.Manual && veiculo.VeiSts == "V") evento = EventoStory.Vendido;

            byte[] jpeg;
            using (var foto = await Image.LoadAsync(arquivoFoto))
                jpeg = StoryArte.Gerar(foto, MontarDados(veiculo, loja, evento, precoAnterior));

            var caminhoRelativo = await GravarArteAsync(jpeg);
            var imageUrl = $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(caminhoRelativo)}";

            var resultado = new StoryPublicacaoResultado();

            if (noInstagram)
            {
                var ig = await _instagramPostService.PublicarStoryAsync(veiculoId, imageUrl);
                if (ig != null)
                {
                    await _publicacaoRepository.CreateAsync(
                        new VeiculoPublicacao(veiculoId, InstagramPostService.PLATAFORMA_STORY, ig.ExternoId, ig.Url));
                    resultado.InstagramUrl = ig.Url;
                }
            }

            if (noFacebook)
            {
                var fb = await _facebookPagePostService.PublicarStoryAsync(veiculoId, imageUrl);
                if (fb != null)
                {
                    await _publicacaoRepository.CreateAsync(
                        new VeiculoPublicacao(veiculoId, FacebookPagePostService.PLATAFORMA_STORY, fb.ExternoId, fb.Url));
                    resultado.FacebookUrl = fb.Url;
                }
            }

            var publicados = new List<string>();
            var falhas = new List<string>();
            if (noInstagram) (resultado.InstagramUrl != null ? publicados : falhas).Add("Instagram");
            if (noFacebook) (resultado.FacebookUrl != null ? publicados : falhas).Add("Facebook");

            resultado.Sucesso = publicados.Count > 0;
            resultado.Mensagem = publicados.Count == 0
                ? "A Meta recusou o story. Veja os logs da integração para o motivo."
                : falhas.Count == 0
                    ? $"Story publicado no {string.Join(" e no ", publicados)}!"
                    : $"Story publicado no {publicados[0]}, mas falhou no {falhas[0]}.";
            return resultado;
        }

        /// <summary>O texto da arte para cada evento. Public para os testes.</summary>
        public static StoryArteDados MontarDados(Veiculo veiculo, Loja? loja, string evento, decimal? precoAnterior)
        {
            var detalhes = new List<string> { veiculo.VeiAno.ToString(CultureInfo.InvariantCulture) };
            if (veiculo.VeiKm > 0) detalhes.Add(veiculo.VeiKm.ToString("N0", PtBr) + " km");
            if (!string.IsNullOrWhiteSpace(veiculo.VeiCor)) detalhes.Add(veiculo.VeiCor.Trim());

            var dados = new StoryArteDados
            {
                Titulo = $"{veiculo.VeiMarca} {veiculo.VeiModelo}".Trim(),
                Detalhes = string.Join("  •  ", detalhes),
                LojaNome = string.IsNullOrWhiteSpace(loja?.LojNome) ? "" : loja!.LojNome.Trim(),
                Contato = string.IsNullOrWhiteSpace(loja?.LojWhatsApp) ? null : "WhatsApp " + loja!.LojWhatsApp.Trim(),
                CorDestaque = loja?.LojCorPrimaria
            };

            switch (evento)
            {
                case EventoStory.Vendido:
                    // Carro vendido nao mostra preco: o story e' prova social, nao oferta.
                    dados.Selo = "Vendido";
                    dados.Mensagem = "Mais um cliente satisfeito!";
                    break;
                case EventoStory.PrecoBaixou:
                    dados.Selo = "Baixou o preço";
                    dados.Preco = FormatarPreco(veiculo.VeiPreco);
                    if (precoAnterior.HasValue && precoAnterior.Value > veiculo.VeiPreco)
                        dados.PrecoAnterior = "De " + FormatarPreco(precoAnterior.Value);
                    break;
                case EventoStory.Novo:
                    dados.Selo = "Acabou de chegar";
                    dados.Preco = FormatarPreco(veiculo.VeiPreco);
                    break;
                default:
                    dados.Selo = veiculo.VeiSts == "R" ? "Reservado" : "À venda";
                    dados.Preco = FormatarPreco(veiculo.VeiPreco);
                    break;
            }

            return dados;
        }

        private static string FormatarPreco(decimal valor)
            => "R$ " + valor.ToString(valor % 1 == 0 ? "N0" : "N2", PtBr);

        private async Task<bool> TeveStoryRecenteAsync(int veiculoId)
        {
            var corte = DateTime.UtcNow.AddHours(-HorasEntreStoriesDePreco);
            return (await _publicacaoRepository.GetByVeiculoIdAsync(veiculoId))
                .Any(p => (p.PubPlataforma == InstagramPostService.PLATAFORMA_STORY
                           || p.PubPlataforma == FacebookPagePostService.PLATAFORMA_STORY)
                          && p.PubDtPublicacao >= corte);
        }

        /// <summary>
        /// Grava a arte onde o /api/imagens/file ja serve arquivos, e aproveita
        /// para apagar as antigas. Devolve o caminho relativo usado na URL.
        /// </summary>
        private async Task<string> GravarArteAsync(byte[] jpeg)
        {
            var pasta = Path.Combine(_environment.ContentRootPath, "uploads", "_stories");
            Directory.CreateDirectory(pasta);

            try
            {
                foreach (var antigo in Directory.EnumerateFiles(pasta, "*.jpg"))
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(antigo) > ValidadeArte)
                        File.Delete(antigo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Falha ao limpar artes antigas de story");
            }

            var nome = $"{Guid.NewGuid():N}.jpg";
            await File.WriteAllBytesAsync(Path.Combine(pasta, nome), jpeg);
            return $"/uploads/_stories/{nome}";
        }

        private string CaminhoNoDisco(string caminhoRelativo)
            => Path.Combine(_environment.ContentRootPath,
                caminhoRelativo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        private async Task<bool> LigadoAsync(string chave)
            => string.Equals(await _configRepository.GetValorAsync(chave), "true", StringComparison.OrdinalIgnoreCase);

        private Task GravarAsync(string chave, bool valor)
            => _configRepository.SetValorAsync(chave, valor ? "true" : "false");

        private static StoryPublicacaoResultado Falha(string mensagem)
            => new() { Sucesso = false, Mensagem = mensagem };

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
