namespace ConnectVeiculos.Core.Interfaces.Services
{
    // Verificador de limites do plano antes de criar recursos no tenant atual.
    // Lanca LimitePlanoException se o tenant esta fora do trial e ja atingiu o
    // limite. Durante trial (TenTrialAte > now), TODOS os limites sao ignorados.
    public interface ILimiteService
    {
        Task GarantirPodeCriarVeiculoAsync(CancellationToken ct = default);
        Task GarantirPodeCriarLojaAsync(CancellationToken ct = default);
        Task GarantirPodeCriarUsuarioAsync(CancellationToken ct = default);
    }
}
