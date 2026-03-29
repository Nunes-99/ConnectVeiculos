namespace ConnectVeiculos.Application.Interfaces.Vendas
{
    public interface IEstornarVendaUseCase
    {
        Task Execute(int vendaId);
    }
}
