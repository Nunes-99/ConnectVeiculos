namespace ConnectVeiculos.Application.Interfaces.Veiculos
{
    public interface IInativarVeiculoUseCase
    {
        Task Execute(int id);
    }
}
