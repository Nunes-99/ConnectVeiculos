using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Publicacoes
{
    /// <inheritdoc cref="IPublicacaoAutomaticaService"/>
    public sealed class PublicacaoAutomaticaService : IPublicacaoAutomaticaService
    {
        // Quanto esperar as fotos aparecerem. O upload vem logo depois do
        // cadastro, mas sao varios arquivos grandes numa conexao de loja, entao
        // a janela e' folgada: 40 x 3s = 2 minutos.
        private const int TentativasPadrao = 40;
        private const int IntervaloPadraoMs = 3000;

        // Quantas leituras seguidas com a mesma contagem indicam que o upload
        // terminou. Sem isso, publicaria com a primeira foto de dez.
        private const int LeiturasEstaveisPadrao = 2;

        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly IVeiculoPublicacaoRepository _publicacaoRepository;
        private readonly IMercadoLivreService _mercadoLivreService;
        private readonly IFacebookCatalogService _facebookCatalogService;
        private readonly IFacebookPagePostService _facebookPagePostService;
        private readonly IInstagramPostService _instagramPostService;
        private readonly IGoogleMerchantService _googleService;
        private readonly ILogger<PublicacaoAutomaticaService> _logger;

        private readonly int _tentativas;
        private readonly int _intervaloMs;
        private readonly int _leiturasEstaveis;

        public PublicacaoAutomaticaService(
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            IVeiculoPublicacaoRepository publicacaoRepository,
            IMercadoLivreService mercadoLivreService,
            IFacebookCatalogService facebookCatalogService,
            IFacebookPagePostService facebookPagePostService,
            IInstagramPostService instagramPostService,
            IGoogleMerchantService googleService,
            ILogger<PublicacaoAutomaticaService> logger)
            : this(veiculoRepository, imagemRepository, publicacaoRepository, mercadoLivreService,
                   facebookCatalogService, facebookPagePostService, instagramPostService,
                   googleService, logger,
                   TentativasPadrao, IntervaloPadraoMs, LeiturasEstaveisPadrao)
        {
        }

        /// <summary>Sobrecarga usada pelos testes pra nao esperar 2 minutos de verdade.</summary>
        public PublicacaoAutomaticaService(
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            IVeiculoPublicacaoRepository publicacaoRepository,
            IMercadoLivreService mercadoLivreService,
            IFacebookCatalogService facebookCatalogService,
            IFacebookPagePostService facebookPagePostService,
            IInstagramPostService instagramPostService,
            IGoogleMerchantService googleService,
            ILogger<PublicacaoAutomaticaService> logger,
            int tentativas,
            int intervaloMs,
            int leiturasEstaveis)
        {
            _veiculoRepository = veiculoRepository;
            _imagemRepository = imagemRepository;
            _publicacaoRepository = publicacaoRepository;
            _mercadoLivreService = mercadoLivreService;
            _facebookCatalogService = facebookCatalogService;
            _facebookPagePostService = facebookPagePostService;
            _instagramPostService = instagramPostService;
            _googleService = googleService;
            _logger = logger;
            _tentativas = tentativas;
            _intervaloMs = intervaloMs;
            _leiturasEstaveis = leiturasEstaveis;
        }

        public async Task PublicarNovoVeiculoAsync(int veiculoId)
        {
            var fotos = await EsperarFotosAsync(veiculoId);
            if (fotos == 0)
            {
                _logger.LogInformation(
                    "Publicacao automatica do veiculo {VeiculoId} nao aconteceu: nenhuma foto foi enviada na janela de espera. "
                    + "O operador pode publicar manualmente depois de subir as fotos.",
                    veiculoId);
                return;
            }

            // A janela de espera e' longa o bastante pro veiculo ter sido apagado
            // ou marcado como vendido/reservado nesse meio tempo.
            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null)
            {
                _logger.LogInformation(
                    "Publicacao automatica do veiculo {VeiculoId} cancelada: veiculo nao existe mais.", veiculoId);
                return;
            }

            if (veiculo.VeiSts != "D")
            {
                _logger.LogInformation(
                    "Publicacao automatica do veiculo {VeiculoId} cancelada: status mudou para {Status} durante a espera.",
                    veiculoId, veiculo.VeiSts);
                return;
            }

            _logger.LogInformation(
                "Publicando veiculo {VeiculoId} nas plataformas externas ({Fotos} foto(s)).", veiculoId, fotos);

            await PublicarMercadoLivreAsync(veiculoId);
            await PublicarFacebookCatalogoAsync(veiculoId);
            await PublicarFacebookPageAsync(veiculoId);
            await PublicarInstagramAsync(veiculoId);
            await PublicarGoogleAsync(veiculoId);
        }

        /// <summary>
        /// Espera as fotos aparecerem e o upload estabilizar. Devolve quantas
        /// fotos havia, ou 0 se nenhuma chegou dentro da janela.
        /// </summary>
        private async Task<int> EsperarFotosAsync(int veiculoId)
        {
            var anterior = -1;
            var repeticoes = 0;

            for (var tentativa = 0; tentativa < _tentativas; tentativa++)
            {
                int atual;
                try
                {
                    atual = (await _imagemRepository.GetByVeiculoIdAsync(veiculoId)).Count(i => i.ImgSts);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao contar fotos do veiculo {VeiculoId}; tentando de novo.", veiculoId);
                    await Task.Delay(_intervaloMs);
                    continue;
                }

                if (atual > 0)
                {
                    // Conta parou de crescer: o upload acabou.
                    if (atual == anterior && ++repeticoes >= _leiturasEstaveis - 1)
                        return atual;

                    if (atual != anterior) repeticoes = 0;
                }

                anterior = atual;
                await Task.Delay(_intervaloMs);
            }

            // Estourou o tempo. Se ja havia foto, publica com o que tem — melhor
            // que perder o post porque o operador continuou subindo imagens.
            return Math.Max(anterior, 0);
        }

        private async Task PublicarMercadoLivreAsync(int veiculoId)
        {
            try
            {
                if (!await _mercadoLivreService.IsConnectedAsync()) return;

                if (await _publicacaoRepository.GetAtivaByVeiculoEPlataformaAsync(veiculoId, "MercadoLivre") != null)
                    return;

                var (externoId, url, aguardandoPagamento) = await _mercadoLivreService.PublicarVeiculoAsync(veiculoId);
                await _publicacaoRepository.CreateAsync(
                    new VeiculoPublicacao(veiculoId, "MercadoLivre", externoId, url, aguardandoPagamento));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar veiculo {VeiculoId} no Mercado Livre", veiculoId);
            }
        }

        private async Task PublicarFacebookCatalogoAsync(int veiculoId)
        {
            try { await _facebookCatalogService.PublicarVeiculoAsync(veiculoId); }
            catch (Exception ex) { _logger.LogError(ex, "Erro ao publicar veiculo {VeiculoId} no Facebook Catalog", veiculoId); }
        }

        private async Task PublicarFacebookPageAsync(int veiculoId)
        {
            try
            {
                if (await _publicacaoRepository.GetAtivaByVeiculoEPlataformaAsync(veiculoId, "FacebookPage") != null)
                    return;

                var r = await _facebookPagePostService.PublicarVeiculoAsync(veiculoId);
                if (r != null)
                    await _publicacaoRepository.CreateAsync(new VeiculoPublicacao(veiculoId, "FacebookPage", r.ExternoId, r.Url));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao postar veiculo {VeiculoId} na Facebook Page", veiculoId);
            }
        }

        private async Task PublicarInstagramAsync(int veiculoId)
        {
            try
            {
                if (await _publicacaoRepository.GetAtivaByVeiculoEPlataformaAsync(veiculoId, "Instagram") != null)
                    return;

                var r = await _instagramPostService.PublicarVeiculoAsync(veiculoId);
                if (r != null)
                    await _publicacaoRepository.CreateAsync(new VeiculoPublicacao(veiculoId, "Instagram", r.ExternoId, r.Url));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao postar veiculo {VeiculoId} no Instagram", veiculoId);
            }
        }

        private async Task PublicarGoogleAsync(int veiculoId)
        {
            try { await _googleService.PublicarVeiculoAsync(veiculoId); }
            catch (Exception ex) { _logger.LogError(ex, "Erro ao publicar veiculo {VeiculoId} no Google", veiculoId); }
        }
    }
}
