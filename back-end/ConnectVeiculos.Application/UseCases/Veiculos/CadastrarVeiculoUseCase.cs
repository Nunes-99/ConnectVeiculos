using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class CadastrarVeiculoUseCase : ICadastrarVeiculoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificacaoService _notificacaoService;
        private readonly ICatalogoHubService _catalogoHubService;
        private readonly ITenantContext _tenantContext;
        private readonly ILimiteService _limiteService;
        private readonly ITenantBackgroundRunner _backgroundRunner;

        public CadastrarVeiculoUseCase(
            IVeiculoRepository veiculoRepository,
            IUnitOfWork unitOfWork,
            INotificacaoService notificacaoService,
            ICatalogoHubService catalogoHubService,
            ITenantContext tenantContext,
            ILimiteService limiteService,
            ITenantBackgroundRunner backgroundRunner)
        {
            _veiculoRepository = veiculoRepository;
            _unitOfWork = unitOfWork;
            _notificacaoService = notificacaoService;
            _catalogoHubService = catalogoHubService;
            _tenantContext = tenantContext;
            _limiteService = limiteService;
            _backgroundRunner = backgroundRunner;
        }

        public async Task<int> Execute(VeiculoInputModel inputModel)
        {
            await _limiteService.GarantirPodeCriarVeiculoAsync();

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
                inputModel.VeiPrecoFipe,
                inputModel.VeiRenavam
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
                    _backgroundRunner.Enqueue<IFavoritoNotificacaoService>(
                        s => s.NotificarVeiculoSimilarAsync(id),
                        $"notificar veiculo similar {id}");
                }

                // IndexNow — notifica Bing/Yandex/DuckDuckGo que existe nova URL
                // (fire-and-forget; falha nao quebra o cadastro). Apenas para
                // veiculos disponiveis no catalogo publico (sts=D).
                if (inputModel.VeiSts == "D")
                {
                    var slugParaIndexNow = _tenantContext.TenantSlug;
                    _backgroundRunner.Enqueue<IIndexNowService>(
                        s => s.NotifyVeiculoAsync(slugParaIndexNow, id),
                        $"IndexNow veiculo {id}");
                }

                // Publicar nas plataformas externas se disponivel.
                //
                // Em background, e nao aqui direto: o cadastro sao duas requisicoes
                // — primeiro o veiculo, depois as fotos, porque o upload precisa do
                // id. Publicando na hora, o veiculo ainda nao tinha imagem nenhuma:
                // o Instagram desistia em "sem imagens" e a Facebook Page postava so
                // o texto. O servico espera as fotos chegarem antes de publicar.
                if (inputModel.VeiSts == "D")
                {
                    _backgroundRunner.Enqueue<IPublicacaoAutomaticaService>(
                        s => s.PublicarNovoVeiculoAsync(id),
                        $"publicar veiculo {id} nas plataformas externas");
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
