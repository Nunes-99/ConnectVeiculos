using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class AtualizarVeiculoUseCase : IAtualizarVeiculoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificacaoService _notificacaoService;
        private readonly ICatalogoHubService _catalogoHubService;

        public AtualizarVeiculoUseCase(
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

        public async Task Execute(VeiculoInputModel inputModel)
        {
            var veiculo = await _veiculoRepository.GetByIdAsync(inputModel.VeiId);

            if (veiculo == null)
                throw new Exception("Veiculo nao encontrado.");

            var statusAnterior = veiculo.VeiSts;

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
                inputModel.VeiObservacao
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

                await _catalogoHubService.NotificarAtualizacaoCatalogo(inputModel.R_LojId, tipoEvento, new
                {
                    veiculoId = inputModel.VeiId,
                    marca = inputModel.VeiMarca,
                    modelo = inputModel.VeiModelo,
                    ano = inputModel.VeiAno,
                    preco = inputModel.VeiPreco,
                    statusAnterior,
                    statusNovo = inputModel.VeiSts
                });
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
