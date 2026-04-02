using ConnectVeiculos.Application.Interfaces.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;

namespace ConnectVeiculos.Application.UseCases.Usuarios
{
    public class InativarUsuarioUseCase : IInativarUsuarioUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IUnitOfWork _unitOfWork;

        public InativarUsuarioUseCase(IUsuarioRepository usuarioRepository, IUnitOfWork unitOfWork)
        {
            _usuarioRepository = usuarioRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(int id)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(id);

            if (usuario == null)
                throw new Exception("Usuário não encontrado.");

            _unitOfWork.BeginTransaction();

            try
            {
                await _usuarioRepository.DeleteAsync(id);
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
