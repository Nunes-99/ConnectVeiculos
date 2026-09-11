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
        private readonly IPublicacaoAutomaticaService _publicacaoAutomaticaService;
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<InativarVeiculoUseCase> _logger;
        private readonly IIndexNowService _indexNowService;
        private readonly ITenantBackgroundRunner _backgroundRunner;

        public InativarVeiculoUseCase(
            IVeiculoRepository veiculoRepository,
            IUnitOfWork unitOfWork,
            ICatalogoHubService catalogoHubService,
            IPublicacaoAutomaticaService publicacaoAutomaticaService,
            ITenantContext tenantContext,
            ILogger<InativarVeiculoUseCase> logger,
            IIndexNowService indexNowService,
            ITenantBackgroundRunner backgroundRunner)
        {
            _veiculoRepository = veiculoRepository;
            _unitOfWork = unitOfWork;
            _catalogoHubService = catalogoHubService;
            _publicacaoAutomaticaService = publicacaoAutomaticaService;
            _tenantContext = tenantContext;
            _logger = logger;
            _indexNowService = indexNowService;
            _backgroundRunner = backgroundRunner;
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
                var slugParaIndexNow = _tenantContext.TenantSlug;
                _backgroundRunner.Enqueue<IIndexNowService>(
                    s => s.NotifyVeiculoAsync(slugParaIndexNow, null),
                    "IndexNow catalogo (veiculo inativado)");

                // Notificar catalogo publico
                await _catalogoHubService.NotificarAtualizacaoCatalogo(_tenantContext.TenantSlug, lojaId, "VEICULO_REMOVIDO", new
                {
                    veiculoId = id
                });

                // Tirar das plataformas. A rotina e a mesma de vender ou
                // reservar; aqui ela tambem carimba o post da Page, coisa que este
                // caso nao fazia — o post seguia anunciando um carro apagado.
                await _publicacaoAutomaticaService.MarcarVeiculoIndisponivelAsync(id, "I");
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
