using ConnectVeiculos.Core.Entities.NotasFiscais;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.NotasFiscais
{
    public interface INotaFiscalRepository
    {
        Task<NotaFiscal?> GetByIdAsync(int id);
        Task<IReadOnlyList<NotaFiscal>> GetByVeiculoAsync(int veiculoId);
        Task<bool> NumeroEmUsoAsync(string ambiente, int serie, int numero);
        Task SalvarAsync(NotaFiscal nota);

        /// <summary>Notas da serie com numero entre inicial e final (inclusive).</summary>
        Task<IReadOnlyList<NotaFiscal>> GetNoIntervaloAsync(string ambiente, int serie, int inicial, int final);
        Task<IReadOnlyList<NotaFiscalInutilizacao>> ListarInutilizacoesAsync();
        Task<NotaFiscalInutilizacao?> ObterInutilizacaoAsync(int id);
        Task SalvarInutilizacaoAsync(NotaFiscalInutilizacao inutilizacao);
    }
}
