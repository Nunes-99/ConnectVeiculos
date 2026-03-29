namespace ConnectVeiculos.Application.Interfaces.Imagens
{
    public interface IExcluirImagemVeiculoUseCase
    {
        Task Execute(int imagemId);
    }
}
