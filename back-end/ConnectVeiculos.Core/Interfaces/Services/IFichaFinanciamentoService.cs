using ConnectVeiculos.Core.Financiamentos;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Ficha unica de financiamento: bancos da loja (com a tabela de taxas de
    /// cada um), estimativa de parcelas em todos de uma vez e o retorno de
    /// cada proposta. Nada aqui consulta banco ou bureau de credito.
    /// Recusas chegam como <see cref="FinanciamentoException"/>.
    /// </summary>
    public interface IFichaFinanciamentoService
    {
        Task<IReadOnlyList<BancoLojaInfo>> ListarBancosAsync();
        Task<BancoLojaInfo> SalvarBancoAsync(BancoLojaInfo input);
        Task RemoverBancoAsync(int bancoId);

        Task<IReadOnlyList<EstimativaBanco>> EstimarAsync(EstimativaInput input);

        /// <param name="vendedorId">Quando informado, so as fichas desse vendedor.</param>
        Task<IReadOnlyList<FichaInfo>> ListarFichasAsync(int? vendedorId);
        Task<FichaInfo> ObterFichaAsync(int fichaId, int? vendedorId);
        Task<FichaInfo> CriarFichaAsync(FichaInput input, int usuarioId);
        Task<FichaInfo> AtualizarFichaAsync(int fichaId, FichaInput input, int? vendedorId);
        Task<FichaInfo> AlterarStatusAsync(int fichaId, string status, int? vendedorId);
        Task<FichaInfo> RegistrarPropostaAsync(int fichaId, PropostaBanco proposta, int? vendedorId);
        Task<FichaInfo> RemoverPropostaAsync(int fichaId, int bancoId, int? vendedorId);
    }
}
