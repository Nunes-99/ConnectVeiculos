namespace ConnectVeiculos.Application.Interfaces.Usuarios
{
    public interface IInativarUsuarioUseCase
    {
        Task Execute(int id);
    }
}
