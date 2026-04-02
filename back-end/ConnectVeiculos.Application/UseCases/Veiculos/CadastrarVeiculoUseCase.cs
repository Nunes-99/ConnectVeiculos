using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class CadastrarVeiculoUseCase : ICadastrarVeiculoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificacaoService _notificacaoService;
        private readonly ICatalogoHubService _catalogoHubService;

        public CadastrarVeiculoUseCase(
            IVeiculoRepository veiculoRepository,
            IUnitOfWork unitOfWork,
            INotificacaoService notificacaoService,
            ICatalogoHubService catalogoHubService)
        {
            _veiculoRepository = veiculoRepository;
            _unitOfWork = unitOfWork;
            _notificacaoService = notificacaoService;
            _catalogoHubService = catalogoHubService;
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
                inputModel.VeiDonoCelular
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
                await _catalogoHubService.NotificarAtualizacaoCatalogo(inputModel.R_LojId, "VEICULO_ADICIONADO", new
                {
                    veiculoId = id,
                    marca = inputModel.VeiMarca,
                    modelo = inputModel.VeiModelo,
                    ano = inputModel.VeiAno,
                    preco = inputModel.VeiPreco
                });

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
