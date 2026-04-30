using ConnectVeiculos.Core.Entities.Publicacoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes
{
    public interface IVeiculoPublicacaoRepository
    {
        Task<VeiculoPublicacao> GetByIdAsync(int id);
        Task<IEnumerable<VeiculoPublicacao>> GetByVeiculoIdAsync(int veiculoId);
        Task<VeiculoPublicacao> GetAtivaByVeiculoEPlataformaAsync(int veiculoId, string plataforma);
        Task<int> CreateAsync(VeiculoPublicacao publicacao);
        Task UpdateAsync(VeiculoPublicacao publicacao);
    }
}
