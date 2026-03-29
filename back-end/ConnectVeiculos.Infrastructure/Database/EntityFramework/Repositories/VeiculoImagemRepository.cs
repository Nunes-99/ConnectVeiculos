using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VeiculoImagemRepository : IVeiculoImagemRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoImagemRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<VeiculoImagem> GetByIdAsync(int id)
        {
            return await _context.VeiculosImagens.FirstOrDefaultAsync(i => i.ImgId == id);
        }

        public async Task<IEnumerable<VeiculoImagem>> GetAllAsync()
        {
            return await _context.VeiculosImagens
                .Where(i => i.ImgSts)
                .OrderBy(i => i.R_VeiId)
                .ThenBy(i => i.ImgOrdem)
                .ToListAsync();
        }

        public async Task<IEnumerable<VeiculoImagem>> GetByVeiculoIdAsync(int veiculoId)
        {
            return await _context.VeiculosImagens
                .Where(i => i.R_VeiId == veiculoId)
                .OrderBy(i => i.ImgOrdem)
                .ToListAsync();
        }

        public async Task<int> CreateAsync(VeiculoImagem imagem)
        {
            _context.VeiculosImagens.Add(imagem);
            await _context.SaveChangesAsync();
            return imagem.ImgId;
        }

        public async Task UpdateAsync(VeiculoImagem imagem)
        {
            _context.VeiculosImagens.Update(imagem);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var imagem = await GetByIdAsync(id);
            if (imagem != null)
            {
                imagem.AlterarStatus(false);
                await UpdateAsync(imagem);
            }
        }
    }
}
