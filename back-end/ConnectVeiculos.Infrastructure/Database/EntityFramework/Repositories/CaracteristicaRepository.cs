using ConnectVeiculos.Core.Entities.Caracteristicas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Caracteristicas;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class CaracteristicaRepository : ICaracteristicaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public CaracteristicaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<Caracteristica> GetByIdAsync(int id)
        {
            return await _context.Caracteristicas.FirstOrDefaultAsync(c => c.CarId == id);
        }

        public async Task<IEnumerable<Caracteristica>> GetAllAsync()
        {
            return await _context.Caracteristicas.ToListAsync();
        }

        public async Task<int> CreateAsync(Caracteristica caracteristica)
        {
            _context.Caracteristicas.Add(caracteristica);
            await _context.SaveChangesAsync();
            return caracteristica.CarId;
        }

        public async Task UpdateAsync(Caracteristica caracteristica)
        {
            _context.Caracteristicas.Update(caracteristica);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var caracteristica = await GetByIdAsync(id);
            if (caracteristica != null)
            {
                caracteristica.AlterarStatus(false);
                await UpdateAsync(caracteristica);
            }
        }
    }
}
