using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class InativarVeiculoUseCase : IInativarVeiculoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICatalogoHubService _catalogoHubService;

        public InativarVeiculoUseCase(
            IVeiculoRepository veiculoRepository,
            IUnitOfWork unitOfWork,
            ICatalogoHubService catalogoHubService)
        {
            _veiculoRepository = veiculoRepository;
            _unitOfWork = unitOfWork;
            _catalogoHubService = catalogoHubService;
        }

        public async Task Execute(int id)
        {
            var veiculo = await _veiculoRepository.GetByIdAsync(id);

            if (veiculo == null)
                throw new Exception("Veiculo nao encontrado.");

            var lojaId = veiculo.R_LojId;
            veiculo.AlterarStatus("I");

            _unitOfWork.BeginTransaction();

            try
            {
                await _veiculoRepository.UpdateAsync(veiculo);
                _unitOfWork.Commit();

                // Notificar catalogo publico
                await _catalogoHubService.NotificarAtualizacaoCatalogo(lojaId, "VEICULO_REMOVIDO", new
                {
                    veiculoId = id
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
