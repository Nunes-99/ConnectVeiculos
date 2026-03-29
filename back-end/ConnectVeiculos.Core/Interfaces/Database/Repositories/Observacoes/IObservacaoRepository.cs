using ConnectVeiculos.Core.Entities.Observacoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Observacoes
{
    public interface IObservacaoRepository
    {
        Task<Observacao> GetByIdAsync(int id);
        Task<IEnumerable<Observacao>> GetAllAsync();
        Task<int> CreateAsync(Observacao observacao);
        Task UpdateAsync(Observacao observacao);
        Task DeleteAsync(int id);
    }
}
