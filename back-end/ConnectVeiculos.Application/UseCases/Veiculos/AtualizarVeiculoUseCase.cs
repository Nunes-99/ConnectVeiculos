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
        private readonly IGoogleMerchantService _googleService;
        private readonly IVeiculoPublicacaoRepository _publicacaoRepository;
        private readonly ILogger<AtualizarVeiculoUseCase> _logger;
        private readonly IFavoritoNotificacaoService _favoritoNotificacaoService;
        private readonly ITenantContext _tenantContext;

        public AtualizarVeiculoUseCase(
            IVeiculoRepository veiculoRepository,
            IUnitOfWork unitOfWork,
            INotificacaoService notificacaoService,
            ICatalogoHubService catalogoHubService,
            IMercadoLivreService mercadoLivreService,
            IFacebookCatalogService facebookService,
            IGoogleMerchantService googleService,
            IVeiculoPublicacaoRepository publicacaoRepository,
            ILogger<AtualizarVeiculoUseCase> logger,
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
            _tenantContext = tenantContext;
            _favoritoNotificacaoService = favoritoNotificacaoService;
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
                inputModel.VeiPrecoFipe
            );

            _unitOfWork.BeginTransaction();

            try
            {
                await _veiculoRepository.UpdateAsync(veiculo);
                _unitOfWork.Commit();

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
                    _ = Task.Run(() => _favoritoNotificacaoService.NotificarPrecoAlteradoAsync(inputModel.VeiId, precoAnterior, inputModel.VeiPreco));
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
                                var (externoId, url) = await _mercadoLivreService.PublicarVeiculoAsync(inputModel.VeiId);
                                await _publicacaoRepository.CreateAsync(new VeiculoPublicacao(inputModel.VeiId, "MercadoLivre", externoId, url));
                            }
                        }

                        try { await _facebookService.PublicarVeiculoAsync(inputModel.VeiId); }
                        catch (Exception ex) { _logger.LogError(ex, "Erro ao publicar no Facebook"); }

                        try { await _googleService.PublicarVeiculoAsync(inputModel.VeiId); }
                        catch (Exception ex) { _logger.LogError(ex, "Erro ao publicar no Google"); }
                    }
                    else if (statusAnterior == "D" && inputModel.VeiSts != "D")
                    {
                        // Veiculo saiu de disponivel: remover de todos
                        var publicacao = await _publicacaoRepository.GetAtivaByVeiculoEPlataformaAsync(inputModel.VeiId, "MercadoLivre");
                        if (publicacao != null)
                        {
                            await _mercadoLivreService.RemoverAnuncioAsync(publicacao.PubExternoId);
                            publicacao.Remover();
                            await _publicacaoRepository.UpdateAsync(publicacao);
                        }

                        try { await _facebookService.RemoverVeiculoAsync(inputModel.VeiId); }
                        catch (Exception ex) { _logger.LogError(ex, "Erro ao remover do Facebook"); }

                        try { await _googleService.RemoverVeiculoAsync(inputModel.VeiId); }
                        catch (Exception ex) { _logger.LogError(ex, "Erro ao remover do Google"); }
                    }
                    else if (statusAnterior == "D" && inputModel.VeiSts == "D")
                    {
                        // Continua disponivel mas pode ter mudado preco/info: atualizar
                        try { await _facebookService.PublicarVeiculoAsync(inputModel.VeiId); } catch { }
                        try { await _googleService.PublicarVeiculoAsync(inputModel.VeiId); } catch { }
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
