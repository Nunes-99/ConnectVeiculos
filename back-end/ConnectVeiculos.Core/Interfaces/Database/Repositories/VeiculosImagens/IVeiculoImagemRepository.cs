using ConnectVeiculos.Core.Entities.VeiculosImagens;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens
{
    public interface IVeiculoImagemRepository
    {
        Task<VeiculoImagem> GetByIdAsync(int id);
        Task<IEnumerable<VeiculoImagem>> GetAllAsync();
        Task<IEnumerable<VeiculoImagem>> GetByVeiculoIdAsync(int veiculoId);
        Task<int> CreateAsync(VeiculoImagem imagem);
        Task UpdateAsync(VeiculoImagem imagem);
        Task DeleteAsync(int id);
    }
}
