using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class AtualizarVeiculoUseCase : IAtualizarVeiculoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificacaoService _notificacaoService;
        private readonly ICatalogoHubService _catalogoHubService;
        private readonly IMercadoLivreService _mercadoLivreService;
        private readonly IFacebookCatalogService _facebookService;
        private readonly IPublicacaoAutomaticaService _publicacaoAutomaticaService;
        private readonly IGoogleMerchantService _googleService;
        private readonly IVeiculoPublicacaoRepository _publicacaoRepository;
        private readonly ILogger<AtualizarVeiculoUseCase> _logger;
        private readonly IFavoritoNotificacaoService _favoritoNotificacaoService;
        private readonly ITenantContext _tenantContext;
        private readonly IIndexNowService _indexNowService;
        private readonly ITenantBackgroundRunner _backgroundRunner;

        public AtualizarVeiculoUseCase(
            IVeiculoRepository veiculoRepository,
            IUnitOfWork unitOfWork,
            INotificacaoService notificacaoService,
            ICatalogoHubService catalogoHubService,
            IMercadoLivreService mercadoLivreService,
            IFacebookCatalogService facebookService,
            IPublicacaoAutomaticaService publicacaoAutomaticaService,
            IGoogleMerchantService googleService,
            IVeiculoPublicacaoRepository publicacaoRepository,
            ILogger<AtualizarVeiculoUseCase> logger,
            IFavoritoNotificacaoService favoritoNotificacaoService,
            ITenantContext tenantContext,
            IIndexNowService indexNowService,
            ITenantBackgroundRunner backgroundRunner)
        {
            _veiculoRepository = veiculoRepository;
            _unitOfWork = unitOfWork;
            _notificacaoService = notificacaoService;
            _catalogoHubService = catalogoHubService;
            _mercadoLivreService = mercadoLivreService;
            _facebookService = facebookService;
            _publicacaoAutomaticaService = publicacaoAutomaticaService;
            _googleService = googleService;
            _publicacaoRepository = publicacaoRepository;
            _logger = logger;
            _tenantContext = tenantContext;
            _favoritoNotificacaoService = favoritoNotificacaoService;
            _indexNowService = indexNowService;
            _backgroundRunner = backgroundRunner;
        }

        public async Task Execute(VeiculoInputModel inputModel)
        {
            var veiculo = await _veiculoRepository.GetByIdAsync(inputModel.VeiId);

            if (veiculo == null)
                throw new Exception("Veículo não encontrado.");

            var statusAnterior = veiculo.VeiSts;
            var precoAnterior = veiculo.VeiPreco;

            veiculo.SetProperties(
                inputModel.VeiId,
                inputModel.R_LojId,
                inputModel.R_CatId,
                inputModel.VeiMarca,
                inputModel.VeiModelo,
                inputModel.VeiAno,
                inputModel.VeiPlaca,
                inputModel.VeiChassi,
                inputModel.VeiCor,
                inputModel.VeiKm,
                inputModel.VeiPreco,
                inputModel.VeiDtEntrada,
                inputModel.VeiSts,
                inputModel.VeiSitSts,
                inputModel.VeiPrecoCompra,
                inputModel.VeiObservacao,
                inputModel.VeiDonoAtual,
                inputModel.VeiDonoCelular,
                inputModel.VeiOpcionais,
                inputModel.VeiPrecoFipe,
                inputModel.VeiRenavam
            );

            _unitOfWork.BeginTransaction();

            try
            {
                await _veiculoRepository.UpdateAsync(veiculo);
                _unitOfWork.Commit();

                // IndexNow — preço/status/foto mudou, pede recrawl. Mesmo se o
                // veiculo virou vendido/reservado vale notificar (a listagem
                // muda). Fire-and-forget; falha nao quebra o update.
                var slugParaIndexNow = _tenantContext.TenantSlug;
                var idParaIndexNow = inputModel.VeiId;
                _backgroundRunner.Enqueue<IIndexNowService>(
                    s => s.NotifyVeiculoAsync(slugParaIndexNow, idParaIndexNow),
                    $"IndexNow veiculo {idParaIndexNow}");

                // Notificar se veiculo foi reservado (usuarios internos)
                if (statusAnterior != "R" && inputModel.VeiSts == "R")
                {
                    await _notificacaoService.EnviarParaTodosAsync("VEICULO_RESERVADO", new
                    {
                        veiculoId = inputModel.VeiId,
                        marca = inputModel.VeiMarca,
                        modelo = inputModel.VeiModelo,
                        ano = inputModel.VeiAno
                    });
                }

                // Notificar catalogo publico sobre qualquer mudanca de status
                var tipoEvento = "VEICULO_ATUALIZADO";
                if (statusAnterior == "D" && inputModel.VeiSts == "V")
                    tipoEvento = "VEICULO_VENDIDO";
                else if (statusAnterior == "D" && inputModel.VeiSts == "R")
                    tipoEvento = "VEICULO_RESERVADO";
                else if (statusAnterior != "D" && inputModel.VeiSts == "D")
                    tipoEvento = "VEICULO_DISPONIVEL";

                await _catalogoHubService.NotificarAtualizacaoCatalogo(_tenantContext.TenantSlug, inputModel.R_LojId, tipoEvento, new
                {
                    veiculoId = inputModel.VeiId,
                    marca = inputModel.VeiMarca,
                    modelo = inputModel.VeiModelo,
                    ano = inputModel.VeiAno,
                    preco = inputModel.VeiPreco,
                    statusAnterior,
                    statusNovo = inputModel.VeiSts
                });

                // Notificar favoritos se preco caiu (fire-and-forget, nao bloqueia o response)
                if (inputModel.VeiPreco < precoAnterior)
                {
                    var idParaNotificar = inputModel.VeiId;
                    var precoNovo = inputModel.VeiPreco;
                    _backgroundRunner.Enqueue<IFavoritoNotificacaoService>(
                        s => s.NotificarPrecoAlteradoAsync(idParaNotificar, precoAnterior, precoNovo),
                        $"notificar queda de preco do veiculo {idParaNotificar}");
                }

                // Integracoes externas
                try
                {
                    if (statusAnterior != "D" && inputModel.VeiSts == "D")
                    {
                        // Veiculo ficou disponivel: publicar
                        if (await _mercadoLivreService.IsConnectedAsync())
                        {
                            var existente = await _publicacaoRepository.GetAtivaByVeiculoEPlataformaAsync(inputModel.VeiId, "MercadoLivre");
                            if (existente == null)
                            {
                                var (externoId, url, aguardandoPagamento) = await _mercadoLivreService.PublicarVeiculoAsync(inputModel.VeiId);
                                await _publicacaoRepository.CreateAsync(new VeiculoPublicacao(inputModel.VeiId, "MercadoLivre", externoId, url, aguardandoPagamento));
                            }
                        }

                        try { await _facebookService.PublicarVeiculoAsync(inputModel.VeiId); }
                        catch (Exception ex) { _logger.LogError(ex, "Erro ao publicar no Facebook"); }

                        try { await _googleService.PublicarVeiculoAsync(inputModel.VeiId); }
                        catch (Exception ex) { _logger.LogError(ex, "Erro ao publicar no Google"); }
                    }
                    else if (statusAnterior == "D" && inputModel.VeiSts != "D")
                    {
                        // Veiculo saiu de disponivel. A rotina vive no
                        // PublicacaoAutomaticaService porque registrar uma venda
                        // chega no mesmo ponto por outro caminho.
                        await _publicacaoAutomaticaService
                            .MarcarVeiculoIndisponivelAsync(inputModel.VeiId, inputModel.VeiSts);
                    }
                    else if (statusAnterior == "D" && inputModel.VeiSts == "D")
                    {
                        // Continua disponivel mas pode ter mudado preco/info: atualizar
                        //
                        // O Mercado Livre estava de fora deste ramo: so Facebook e
                        // Google eram atualizados. Na pratica, mudar o preco aqui
                        // deixava o anuncio do ML com o valor antigo indefinidamente,
                        // porque a integracao so agia na transicao pra "disponivel".
                        try
                        {
                            var publicacaoAtual = await _publicacaoRepository
                                .GetAtivaByVeiculoEPlataformaAsync(inputModel.VeiId, "MercadoLivre");

                            if (publicacaoAtual != null && await _mercadoLivreService.IsConnectedAsync())
                                await _mercadoLivreService.AtualizarAnuncioAsync(publicacaoAtual.PubExternoId, inputModel.VeiId);
                        }
                        catch (Exception ex) { _logger.LogError(ex, "Erro ao atualizar anuncio no Mercado Livre"); }

                        try { await _facebookService.PublicarVeiculoAsync(inputModel.VeiId); } catch { }
                        try { await _googleService.PublicarVeiculoAsync(inputModel.VeiId); } catch { }

                        // O preco fica escrito na legenda do post da Page. Sem
                        // reescrever, baixar o preco aqui deixava o Facebook
                        // anunciando o valor antigo pra sempre.
                        if (inputModel.VeiPreco != precoAnterior)
                        {
                            await _publicacaoAutomaticaService
                                .AtualizarPostDoVeiculoAsync(inputModel.VeiId, inputModel.VeiSts);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro na integracao externa para veiculo {VeiculoId}", inputModel.VeiId);
                }
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
