using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class InativarVeiculoUseCase : IInativarVeiculoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICatalogoHubService _catalogoHubService;
        private readonly IMercadoLivreService _mercadoLivreService;
        private readonly IFacebookCatalogService _facebookService;
        private readonly IGoogleMerchantService _googleService;
        private readonly IVeiculoPublicacaoRepository _publicacaoRepository;
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<InativarVeiculoUseCase> _logger;
        private readonly IIndexNowService _indexNowService;

        public InativarVeiculoUseCase(
            IVeiculoRepository veiculoRepository,
            IUnitOfWork unitOfWork,
            ICatalogoHubService catalogoHubService,
            IMercadoLivreService mercadoLivreService,
            IFacebookCatalogService facebookService,
            IGoogleMerchantService googleService,
            IVeiculoPublicacaoRepository publicacaoRepository,
            ITenantContext tenantContext,
            ILogger<InativarVeiculoUseCase> logger,
            IIndexNowService indexNowService)
        {
            _veiculoRepository = veiculoRepository;
            _unitOfWork = unitOfWork;
            _catalogoHubService = catalogoHubService;
            _mercadoLivreService = mercadoLivreService;
            _facebookService = facebookService;
            _googleService = googleService;
            _publicacaoRepository = publicacaoRepository;
            _tenantContext = tenantContext;
            _logger = logger;
            _indexNowService = indexNowService;
        }

        public async Task Execute(int id)
        {
            var veiculo = await _veiculoRepository.GetByIdAsync(id);

            if (veiculo == null)
                throw new Exception("Veículo não encontrado.");

            var lojaId = veiculo.R_LojId;
            veiculo.AlterarStatus("I");

            _unitOfWork.BeginTransaction();

            try
            {
                await _veiculoRepository.UpdateAsync(veiculo);
                _unitOfWork.Commit();

                // IndexNow — veiculo saiu do catalogo publico, pede recrawl
                // pra remover do indice. Notifica apenas a home (sem veiculoId)
                // pra o crawler perceber a remocao via 404.
                _ = Task.Run(() => _indexNowService.NotifyVeiculoAsync(_tenantContext.TenantSlug, null));

                // Notificar catalogo publico
                await _catalogoHubService.NotificarAtualizacaoCatalogo(_tenantContext.TenantSlug, lojaId, "VEICULO_REMOVIDO", new
                {
                    veiculoId = id
                });

                // Remover anuncio do ML se existir
                try
                {
                    var publicacao = await _publicacaoRepository.GetAtivaByVeiculoEPlataformaAsync(id, "MercadoLivre");
                    if (publicacao != null)
                    {
                        await _mercadoLivreService.RemoverAnuncioAsync(publicacao.PubExternoId);
                        publicacao.Remover();
                        await _publicacaoRepository.UpdateAsync(publicacao);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao remover anuncio ML do veiculo {VeiculoId}", id);
                }

                // Remover do Facebook
                try { await _facebookService.RemoverVeiculoAsync(id); }
                catch (Exception ex) { _logger.LogError(ex, "Erro ao remover do Facebook"); }

                // Remover do Google
                try { await _googleService.RemoverVeiculoAsync(id); }
                catch (Exception ex) { _logger.LogError(ex, "Erro ao remover do Google"); }
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
