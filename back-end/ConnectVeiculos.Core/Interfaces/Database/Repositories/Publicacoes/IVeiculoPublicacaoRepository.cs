using ConnectVeiculos.Core.Entities.Publicacoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes
{
    public interface IVeiculoPublicacaoRepository
    {
        Task<VeiculoPublicacao> GetByIdAsync(int id);
        Task<IEnumerable<VeiculoPublicacao>> GetByVeiculoIdAsync(int veiculoId);
        Task<VeiculoPublicacao> GetAtivaByVeiculoEPlataformaAsync(int veiculoId, string plataforma);
         // Usado pelo webhook ML pra mapear notificacao (resource=/items/MLBxxx) de volta
         // pra publicacao local. Retorna null se o ID externo nao bate com nada nosso
         // (anuncio criado fora do sistema ou ja excluido).
         Task<VeiculoPublicacao> GetAtivaByExternoIdAsync(string externoId, string plataforma);
        Task<int> CreateAsync(VeiculoPublicacao publicacao);
        Task UpdateAsync(VeiculoPublicacao publicacao);
    }
}
