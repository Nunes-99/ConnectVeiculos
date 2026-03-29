using ConnectVeiculos.Application.ViewModels.Usuarios;

namespace ConnectVeiculos.Application.Interfaces.Usuarios
{
    public interface IConsultarUsuariosUseCase
    {
        Task<List<UsuarioViewModel>> Execute(string pesquisa, string inicio, string intervalo);
    }
}
