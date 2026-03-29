namespace ConnectVeiculos.Application.Interfaces.Lojas
{
    public interface IInativarLojaUseCase
    {
        Task Execute(int id);
    }
}
