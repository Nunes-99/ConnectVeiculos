namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IFavoritoNotificacaoService
    {
        Task NotificarPrecoAlteradoAsync(int veiculoId, decimal precoAntigo, decimal precoNovo);
        Task NotificarVeiculoSimilarAsync(int veiculoId);
    }
}
