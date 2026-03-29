using ConnectVeiculos.Application.InputModels.Usuarios;

namespace ConnectVeiculos.Application.Interfaces.Usuarios
{
    public interface IAtualizarUsuarioUseCase
    {
        Task Execute(UsuarioInputModel inputModel);
    }
}
