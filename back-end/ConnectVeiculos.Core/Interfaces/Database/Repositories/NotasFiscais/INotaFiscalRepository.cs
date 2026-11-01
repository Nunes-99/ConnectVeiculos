using ConnectVeiculos.Core.Entities.NotasFiscais;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.NotasFiscais
{
    public interface INotaFiscalRepository
    {
        Task<NotaFiscal?> GetByIdAsync(int id);
        Task<IReadOnlyList<NotaFiscal>> GetByVeiculoAsync(int veiculoId);
        Task<bool> NumeroEmUsoAsync(string ambiente, int serie, int numero);
        Task SalvarAsync(NotaFiscal nota);
    }
}
