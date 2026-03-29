using ConnectVeiculos.Core.Entities.Caracteristicas;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Caracteristicas
{
    public interface ICaracteristicaRepository
    {
        Task<Caracteristica> GetByIdAsync(int id);
        Task<IEnumerable<Caracteristica>> GetAllAsync();
        Task<int> CreateAsync(Caracteristica caracteristica);
        Task UpdateAsync(Caracteristica caracteristica);
        Task DeleteAsync(int id);
    }
}
