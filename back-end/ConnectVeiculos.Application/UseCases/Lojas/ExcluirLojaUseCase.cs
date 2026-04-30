using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;

namespace ConnectVeiculos.Application.UseCases.Lojas
{
    public class ExcluirLojaUseCase : IExcluirLojaUseCase
    {
        private readonly ILojaRepository _lojaRepository;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly ILojaUsuarioRepository _lojaUsuarioRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ExcluirLojaUseCase(
            ILojaRepository lojaRepository,
            IVeiculoRepository veiculoRepository,
            ILojaUsuarioRepository lojaUsuarioRepository,
            IUnitOfWork unitOfWork)
        {
            _lojaRepository = lojaRepository;
            _veiculoRepository = veiculoRepository;
            _lojaUsuarioRepository = lojaUsuarioRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(int id)
        {
            var loja = await _lojaRepository.GetByIdAsync(id);

            if (loja == null)
                throw new LojaException("Loja nao encontrada.");

            var veiculos = await _veiculoRepository.GetByLojaIdAsync(id);
            if (veiculos != null && veiculos.Any())
                throw new LojaException($"Nao e possivel excluir a loja: existem {veiculos.Count()} veiculo(s) vinculado(s).");

            _unitOfWork.BeginTransaction();

            try
            {
                await _lojaUsuarioRepository.DeleteByLojaIdAsync(id);
                await _lojaRepository.DeleteAsync(id);
                _unitOfWork.Commit();
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
