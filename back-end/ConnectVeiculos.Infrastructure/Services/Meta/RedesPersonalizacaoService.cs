using ConnectVeiculos.Core.Catalogo;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Services.Stories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ConnectVeiculos.Infrastructure.Services.Meta
{
    /// <inheritdoc cref="IRedesPersonalizacaoService"/>
    public class RedesPersonalizacaoService : IRedesPersonalizacaoService
    {
        public const string KEY_LEGENDA_INSTAGRAM = "META_LEGENDA_INSTAGRAM";
        public const string KEY_LEGENDA_FACEBOOK = "META_LEGENDA_FACEBOOK";
        public const string KEY_CAPA_POST = "META_CAPA_POST";
        public const string KEY_CAPA_STORY = "META_CAPA_STORY";

        // A capa e' redimensionada na chegada: a maior publicacao e' o story
        // (1080x1920), entao guardar mais que isso so ocuparia disco.
        private const int LadoMaximoCapa = 1920;

        private readonly IConfiguracaoSistemaRepository _config;
        private readonly IVeiculoRepository _veiculos;
        private readonly IVeiculoImagemRepository _imagens;
        private readonly ILojaRepository _lojas;
        private readonly IHostEnvironment _environment;
        private readonly MetaSettings _settings;
        private readonly ITenantContext? _tenant;
        private readonly ILogger<RedesPersonalizacaoService> _logger;

        public RedesPersonalizacaoService(
            IConfiguracaoSistemaRepository config,
            IVeiculoRepository veiculos,
            IVeiculoImagemRepository imagens,
            ILojaRepository lojas,
            IHostEnvironment environment,
            IOptions<MetaSettings> settings,
            ILogger<RedesPersonalizacaoService> logger,
            ITenantContext? tenant = null)
        {
            _config = config;
            _veiculos = veiculos;
            _imagens = imagens;
            _lojas = lojas;
            _environment = environment;
            _settings = settings.Value;
            _logger = logger;
            _tenant = tenant;
        }

        public async Task<RedesPersonalizacaoInfo> GetAsync() => new()
        {
            LegendaInstagram = await _config.GetValorAsync(KEY_LEGENDA_INSTAGRAM) ?? "",
            LegendaFacebook = await _config.GetValorAsync(KEY_LEGENDA_FACEBOOK) ?? "",
            LegendaPadraoInstagram = LegendaPost.Padrao(LegendaPost.Instagram),
            LegendaPadraoFacebook = LegendaPost.Padrao(LegendaPost.Facebook),
            CapaPost = await CapaAsync(TipoArte.Post),
            CapaStory = await CapaAsync(TipoArte.Story),
            TamanhoMaximoLegenda = LegendaPost.TamanhoMaximo,
            Variaveis = LegendaPost.Variaveis.ToList(),
            Layouts = StoryArte.Layouts.ToList()
        };

        public async Task SalvarLegendasAsync(string? legendaInstagram, string? legendaFacebook)
        {
            await _config.SetValorAsync(KEY_LEGENDA_INSTAGRAM, Limpar(legendaInstagram));
            await _config.SetValorAsync(KEY_LEGENDA_FACEBOOK, Limpar(legendaFacebook));
            _logger.LogInformation("Modelos de legenda das redes atualizados pelo operador.");
        }

        /// <summary>
        /// Texto igual ao padrao e' gravado vazio: assim, se o padrao do sistema
        /// melhorar, a loja que nunca mudou nada recebe a melhoria.
        /// </summary>
        private static string Limpar(string? texto)
        {
            var t = (texto ?? "").Replace("\r\n", "\n").Trim();
            if (t == LegendaPost.Padrao(LegendaPost.Instagram) || t == LegendaPost.Padrao(LegendaPost.Facebook)) return "";
            return t.Length <= LegendaPost.TamanhoMaximo ? t : t[..LegendaPost.TamanhoMaximo];
        }

        public async Task<string> SalvarCapaAsync(string tipo, Stream conteudo)
        {
            if (!TipoArte.Valido(tipo)) throw new ArgumentException("Tipo de arte inválido.");

            Image imagem;
            try { imagem = await Image.LoadAsync(conteudo); }
            catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
            {
                throw new ArgumentException("O arquivo não é uma imagem válida.");
            }

            using (imagem)
            {
                if (imagem.Width < 320 || imagem.Height < 320)
                    throw new ArgumentException("A imagem é pequena demais. Envie pelo menos 1080 pixels de largura.");

                imagem.Mutate(x => x.AutoOrient());
                if (imagem.Width > LadoMaximoCapa || imagem.Height > LadoMaximoCapa)
                    imagem.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(LadoMaximoCapa, LadoMaximoCapa) }));

                var pasta = Path.Combine(_environment.ContentRootPath, "uploads", "_redes");
                Directory.CreateDirectory(pasta);
                var nome = $"capa-{tipo}-{Guid.NewGuid():N}.jpg";
                await imagem.SaveAsJpegAsync(Path.Combine(pasta, nome), new JpegEncoder { Quality = 90 });

                var anterior = await CapaAsync(tipo);
                var caminho = $"/uploads/_redes/{nome}";
                await _config.SetValorAsync(Chave(tipo), caminho);
                ApagarArquivo(anterior);

                _logger.LogInformation("Capa do {Tipo} das redes trocada pelo operador.", tipo);
                return caminho;
            }
        }

        public async Task RemoverCapaAsync(string tipo)
        {
            if (!TipoArte.Valido(tipo)) throw new ArgumentException("Tipo de arte inválido.");
            var anterior = await CapaAsync(tipo);
            await _config.SetValorAsync(Chave(tipo), "");
            ApagarArquivo(anterior);
        }

        public async Task<string> ModeloLegendaAsync(string rede)
        {
            var proprio = await _config.GetValorAsync(
                rede == LegendaPost.Facebook ? KEY_LEGENDA_FACEBOOK : KEY_LEGENDA_INSTAGRAM);
            return string.IsNullOrWhiteSpace(proprio) ? LegendaPost.Padrao(rede) : proprio;
        }

        public async Task<string> MontarLegendaAsync(int? veiculoId, string rede, string? modelo = null)
        {
            var veiculo = veiculoId.HasValue
                ? await _veiculos.GetByIdAsync(veiculoId.Value)
                : (await _veiculos.GetAllAsync()).Where(v => v.VeiSts == "D").MaxBy(v => v.VeiId) ?? CarroFicticio();
            if (veiculo == null) return "";
            var loja = await _lojas.GetByIdAsync(veiculo.R_LojId);
            var baseUrl = ArtesTemporarias.NormalizarBaseUrl(loja?.LojUrlCatalogo)
                          ?? ArtesTemporarias.NormalizarBaseUrl(_settings?.PublicSiteUrl);
            var slug = _tenant is { IsResolved: true } ? _tenant.TenantSlug : null;
            var link = baseUrl == null ? "" : CatalogoUrl.Veiculo(baseUrl, slug, veiculo.VeiId);
            return LegendaPost.Montar(modelo ?? await ModeloLegendaAsync(rede), rede, veiculo, loja, link);
        }

        // Loja sem nenhum carro ainda: a previa mostra um exemplo em vez de vazio.
        private static Veiculo CarroFicticio()
            => new(0, 0, 0, "Toyota", "Corolla XEi", 2022, "", "", "Prata", 35000, 89900m, DateTime.Today, "D", "", 0m, "");

        public async Task<string?> CapaDoPostAsync(int veiculoId, string baseUrl)
        {
            try
            {
                var arte = await ArteDoPostAsync(veiculoId);
                return arte == null ? null : await ArtesTemporarias.GravarAsync(_environment.ContentRootPath, arte, baseUrl, _logger);
            }
            catch (Exception ex)
            {
                // Sem capa o post ainda sai, so com as fotos.
                _logger.LogWarning(ex, "Nao consegui montar a capa do post do veiculo {VeiculoId}", veiculoId);
                return null;
            }
        }

        private async Task<byte[]?> ArteDoPostAsync(int veiculoId)
        {
            var capa = await CapaNoDiscoAsync(TipoArte.Post);
            if (capa != null)
            {
                using var img = await Image.LoadAsync(capa);
                return StoryArte.GerarCapa(img, StoryArte.LarguraFeed, StoryArte.AlturaFeed);
            }

            var veiculo = await _veiculos.GetByIdAsync(veiculoId);
            if (veiculo == null) return null;
            var fotos = await FotosNoDiscoAsync(veiculoId);
            if (fotos.Count == 0) return null;

            var loja = await _lojas.GetByIdAsync(veiculo.R_LojId);
            using var foto = await Image.LoadAsync(fotos[0]);
            return StoryArte.GerarFeed(foto, StoryService.MontarDados(veiculo, loja, EventoStory.Manual, null));
        }

        public async Task<byte[]?> PreviaArteAsync(string tipo, int? layout, int? veiculoId)
        {
            var veiculo = veiculoId.HasValue
                ? await _veiculos.GetByIdAsync(veiculoId.Value)
                : await CarroDeExemploAsync();

            if (tipo == TipoArte.Post)
            {
                if (veiculo == null && await CapaNoDiscoAsync(TipoArte.Post) == null) return null;
                var post = veiculo == null ? await CapaSoltaAsync(TipoArte.Post) : await ArteDoPostAsync(veiculo.VeiId);
                return post == null ? null : StoryArte.Miniatura(post, 540);
            }

            if (layout == null) return await CapaSoltaAsync(TipoArte.Story) is { } capa ? StoryArte.Miniatura(capa, 540) : null;

            if (veiculo == null) return null;
            var fotos = await FotosNoDiscoAsync(veiculo.VeiId);
            if (fotos.Count == 0) return null;
            var loja = await _lojas.GetByIdAsync(veiculo.R_LojId);
            // Carro escolhido (janela de publicar) sai com o selo dele; o exemplo
            // de Integracoes, como carro novo.
            var evento = !veiculoId.HasValue ? EventoStory.Novo
                : veiculo.VeiSts == "V" ? EventoStory.Vendido : EventoStory.Manual;
            var dados = StoryService.MontarDados(veiculo, loja, evento, null);

            var imagens = new List<Image>();
            try
            {
                foreach (var f in fotos.Take(StoryArte.FotosDoLayout(layout.Value)))
                    imagens.Add(await Image.LoadAsync(f));
                return StoryArte.Miniatura(StoryArte.GerarLayout(layout.Value, imagens, dados), 540);
            }
            finally
            {
                foreach (var i in imagens) i.Dispose();
            }
        }

        private async Task<byte[]?> CapaSoltaAsync(string tipo)
        {
            var capa = await CapaNoDiscoAsync(tipo);
            if (capa == null) return null;
            using var img = await Image.LoadAsync(capa);
            return tipo == TipoArte.Post
                ? StoryArte.GerarCapa(img, StoryArte.LarguraFeed, StoryArte.AlturaFeed)
                : StoryArte.GerarCapa(img, StoryArte.Largura, StoryArte.Altura);
        }

        /// <summary>Carro disponivel mais recente que tenha foto no disco.</summary>
        private async Task<Veiculo?> CarroDeExemploAsync()
        {
            var candidatos = (await _veiculos.GetAllAsync())
                .Where(v => v.VeiSts == "D")
                .OrderByDescending(v => v.VeiId)
                .Take(20);
            foreach (var v in candidatos)
                if ((await FotosNoDiscoAsync(v.VeiId)).Count > 0) return v;
            return null;
        }

        private async Task<List<string>> FotosNoDiscoAsync(int veiculoId)
            => ((await _imagens.GetByVeiculoIdAsync(veiculoId)) ?? Enumerable.Empty<VeiculoImagem>())
                .Where(i => i.ImgSts)
                .OrderBy(i => i.ImgOrdem)
                .Select(i => ArtesTemporarias.NoDisco(_environment.ContentRootPath, i.ImgCaminho))
                .Where(File.Exists)
                .ToList();

        private async Task<string?> CapaAsync(string tipo)
        {
            var v = await _config.GetValorAsync(Chave(tipo));
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        /// <summary>Arquivo da capa no disco, ou null se nao ha capa (ou o arquivo sumiu).</summary>
        public async Task<string?> CapaNoDiscoAsync(string tipo)
        {
            var caminho = await CapaAsync(tipo);
            if (caminho == null) return null;
            var arquivo = ArtesTemporarias.NoDisco(_environment.ContentRootPath, caminho);
            return File.Exists(arquivo) ? arquivo : null;
        }

        private void ApagarArquivo(string? caminho)
        {
            if (string.IsNullOrWhiteSpace(caminho) || !caminho.StartsWith("/uploads/_redes/", StringComparison.Ordinal)) return;
            try
            {
                var arquivo = ArtesTemporarias.NoDisco(_environment.ContentRootPath, caminho);
                if (File.Exists(arquivo)) File.Delete(arquivo);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Nao consegui apagar a capa antiga {Caminho}", caminho);
            }
        }

        private static string Chave(string tipo) => tipo == TipoArte.Story ? KEY_CAPA_STORY : KEY_CAPA_POST;
    }
}
