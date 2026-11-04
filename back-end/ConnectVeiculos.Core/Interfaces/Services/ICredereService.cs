using ConnectVeiculos.Core.Financiamentos;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Credere (financiamento multibanco). A loja assina a Credere e conecta a
    /// conta dela aqui (OAuth); a simulacao vai aos bancos em que a loja ja e
    /// credenciada e volta com parcela, taxa e pre-aprovacao de cada um.
    /// Recusas chegam como <see cref="CredereException"/>.
    /// </summary>
    public interface ICredereService
    {
        Task<CredereStatus> StatusAsync(CancellationToken ct = default);

        /// <summary>URL da tela de login da Credere (com o state anti-CSRF).</summary>
        string UrlAutorizacao();

        Task<CredereStatus> ConectarAsync(string code, string? state, CancellationToken ct = default);
        Task<CredereStatus> EscolherLojaAsync(int lojaId, CancellationToken ct = default);
        Task DesconectarAsync(CancellationToken ct = default);

        Task<IReadOnlyList<CredereModeloVeiculo>> BuscarModelosAsync(string busca, int? ano, CancellationToken ct = default);

        /// <param name="vendedorId">Quando informado, so fichas desse vendedor.</param>
        Task<IReadOnlyList<CredereCampoExigido>> CamposExigidosAsync(int fichaId, int? vendedorId, CancellationToken ct = default);
        Task<CredereSimulacao> SimularAsync(int fichaId, CredereSimulacaoInput input, int? vendedorId, string? emailUsuario,
            CancellationToken ct = default);
        Task<CredereSimulacao> ObterSimulacaoAsync(int fichaId, int? vendedorId, CancellationToken ct = default);
    }
}
