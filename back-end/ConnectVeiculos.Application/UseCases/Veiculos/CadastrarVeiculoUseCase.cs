using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class CadastrarVeiculoUseCase : ICadastrarVeiculoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificacaoService _notificacaoService;
        private readonly ICatalogoHubService _catalogoHubService;
        private readonly IMercadoLivreService _mercadoLivreService;
        private readonly IFacebookCatalogService _facebookService;
        private readonly IGoogleMerchantService _googleService;
        private readonly IVeiculoPublicacaoRepository _publicacaoRepository;
        private readonly ILogger<CadastrarVeiculoUseCase> _logger;
        private readonly IFavoritoNotificacaoService _favoritoNotificacaoService;
        private readonly ITenantContext _tenantContext;

        public CadastrarVeiculoUseCase(
            IVeiculoRepository veiculoRepository,
            IUnitOfWork unitOfWork,
            INotificacaoService notificacaoService,
            ICatalogoHubService catalogoHubService,
            IMercadoLivreService mercadoLivreService,
            IFacebookCatalogService facebookService,
            IGoogleMerchantService googleService,
            IVeiculoPublicacaoRepository publicacaoRepository,
            ILogger<CadastrarVeiculoUseCase> logger,
            IFavoritoNotificacaoService favoritoNotificacaoService,
            ITenantContext tenantContext)
        {
            _veiculoRepository = veiculoRepository;
            _unitOfWork = unitOfWork;
            _notificacaoService = notificacaoService;
            _catalogoHubService = catalogoHubService;
            _mercadoLivreService = mercadoLivreService;
            _facebookService = facebookService;
            _googleService = googleService;
            _publicacaoRepository = publicacaoRepository;
            _logger = logger;
            _favoritoNotificacaoService = favoritoNotificacaoService;
            _tenantContext = tenantContext;
        }

        public async Task<int> Execute(VeiculoInputModel inputModel)
        {
            var veiculo = new Veiculo(
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
                inputModel.VeiDtEntrada == DateTime.MinValue ? DateTime.Now : inputModel.VeiDtEntrada,
                inputModel.VeiSts,
                inputModel.VeiSitSts,
                inputModel.VeiPrecoCompra,
                inputModel.VeiObservacao,
                inputModel.VeiDonoAtual,
                inputModel.VeiDonoCelular,
                inputModel.VeiOpcionais,
                inputModel.VeiPrecoFipe
            );

            _unitOfWork.BeginTransaction();

            try
            {
                var id = await _veiculoRepository.CreateAsync(veiculo);
                _unitOfWork.Commit();

                // Enviar notificacao em tempo real (usuarios internos)
                await _notificacaoService.EnviarParaTodosAsync("NOVO_VEICULO", new
                {
                    veiculoId = id,
                    marca = inputModel.VeiMarca,
                    modelo = inputModel.VeiModelo,
                    ano = inputModel.VeiAno,
                    preco = inputModel.VeiPreco
                });

                // Notificar catalogo publico (visitantes)
                await _catalogoHubService.NotificarAtualizacaoCatalogo(_tenantContext.TenantSlug, inputModel.R_LojId, "VEICULO_ADICIONADO", new
                {
                    veiculoId = id,
                    marca = inputModel.VeiMarca,
                    modelo = inputModel.VeiModelo,
                    ano = inputModel.VeiAno,
                    preco = inputModel.VeiPreco
                });

                // Notificar quem favoritou veiculos similares (fire-and-forget)
                if (inputModel.VeiSts == "D")
                {
                    _ = Task.Run(() => _favoritoNotificacaoService.NotificarVeiculoSimilarAsync(id));
                }

                // Publicar nas plataformas externas se disponivel
                if (inputModel.VeiSts == "D")
                {
                    try
                    {
                        if (await _mercadoLivreService.IsConnectedAsync())
                        {
                            var (externoId, url) = await _mercadoLivreService.PublicarVeiculoAsync(id);
                            await _publicacaoRepository.CreateAsync(new VeiculoPublicacao(id, "MercadoLivre", externoId, url));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao publicar veiculo {VeiculoId} no ML", id);
                    }

                    try { await _facebookService.PublicarVeiculoAsync(id); }
                    catch (Exception ex) { _logger.LogError(ex, "Erro ao publicar veiculo {VeiculoId} no Facebook", id); }

                    try { await _googleService.PublicarVeiculoAsync(id); }
                    catch (Exception ex) { _logger.LogError(ex, "Erro ao publicar veiculo {VeiculoId} no Google", id); }
                }

                return id;
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
