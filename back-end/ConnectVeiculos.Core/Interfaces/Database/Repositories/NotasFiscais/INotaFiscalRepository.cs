using ConnectVeiculos.Core.Entities.NotasFiscais;
using ConnectVeiculos.Core.NotasFiscais;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.NotasFiscais
{
    /// <summary>Nota e o veiculo dela (nulos se o veiculo foi excluido).</summary>
    public record NotaFiscalComVeiculo(NotaFiscal Nota, string? Marca, string? Modelo, short? Ano, string? Placa);

    public interface INotaFiscalRepository
    {
        Task<NotaFiscal?> GetByIdAsync(int id);
        Task<IReadOnlyList<NotaFiscal>> GetByVeiculoAsync(int veiculoId);

        /// <summary>Notas de todos os veiculos, mais recentes primeiro, com o veiculo de cada uma.</summary>
        Task<IReadOnlyList<NotaFiscalComVeiculo>> ListarAsync(NfeFiltro filtro, int limite);
        Task<bool> NumeroEmUsoAsync(string ambiente, int serie, int numero);
        Task SalvarAsync(NotaFiscal nota);

        /// <summary>Notas da serie com numero entre inicial e final (inclusive).</summary>
        Task<IReadOnlyList<NotaFiscal>> GetNoIntervaloAsync(string ambiente, int serie, int inicial, int final);
        Task<IReadOnlyList<NotaFiscalInutilizacao>> ListarInutilizacoesAsync();
        Task<NotaFiscalInutilizacao?> ObterInutilizacaoAsync(int id);
        Task SalvarInutilizacaoAsync(NotaFiscalInutilizacao inutilizacao);
    }
}
