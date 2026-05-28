namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IIndexNowService
    {
         // Notifica os buscadores (Bing/Yandex/DuckDuckGo) que uma página de
         // veículo mudou. Chamar fire-and-forget após CRUD de veículo.
         // tenantSlug e' o slug publico do tenant dono do veiculo. veiculoId
         // pode ser null pra apenas notificar a home do catálogo (ex: ao
         // excluir um veiculo, ainda vale revalidar a listagem).
         Task NotifyVeiculoAsync(string tenantSlug, int? veiculoId, CancellationToken ct = default);
    }
}
