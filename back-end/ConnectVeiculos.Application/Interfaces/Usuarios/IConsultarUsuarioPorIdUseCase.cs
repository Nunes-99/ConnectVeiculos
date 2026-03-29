using ConnectVeiculos.Application.ViewModels.Usuarios;

namespace ConnectVeiculos.Application.Interfaces.Usuarios
{
    public interface IConsultarUsuarioPorIdUseCase
    {
        Task<UsuarioViewModel> Execute(int id);
    }
}
