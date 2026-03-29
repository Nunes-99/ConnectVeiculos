using ConnectVeiculos.Application.InputModels.Usuarios;

namespace ConnectVeiculos.Application.Interfaces.Usuarios
{
    public interface ICadastrarUsuarioUseCase
    {
        Task<int> Execute(UsuarioInputModel inputModel);
    }
}
