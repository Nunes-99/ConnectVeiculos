using ConnectVeiculos.Core.Entities.Tenants;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Tenants
{
    public interface IPlanoRepository
    {
        Task<Plano?> GetByIdAsync(int planoId, CancellationToken ct = default);
        Task<IReadOnlyList<Plano>> ListarAtivosAsync(CancellationToken ct = default);
    }
}
