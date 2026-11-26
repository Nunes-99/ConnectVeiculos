using ConnectVeiculos.Core.Olx;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Anuncios da loja na OLX (API de importacao "autoupload", carros). A
    /// loja conecta a conta dela por OAuth; o ConnectVeiculos e' o integrador.
    /// A conta da loja precisa de um plano profissional da OLX. Erros que a
    /// tela mostra chegam como <see cref="OlxException"/>.
    /// </summary>
    public interface IOlxService
    {
        Task<OlxStatus> StatusAsync(CancellationToken ct = default);
        string UrlAutorizacao();
        Task<OlxStatus> ConectarAsync(string code, string? state, CancellationToken ct = default);
        Task DesconectarAsync(CancellationToken ct = default);

        // Catalogo da OLX (igual para todas as lojas, mas pede o token de uma).
        Task<IReadOnlyList<OlxItem>> MarcasAsync(CancellationToken ct = default);
        Task<IReadOnlyList<OlxItem>> ModelosAsync(string marcaId, CancellationToken ct = default);
        Task<IReadOnlyList<OlxItem>> VersoesAsync(string marcaId, string modeloId, CancellationToken ct = default);
        OlxAtributos Atributos();

        /// <summary>Anuncio do veiculo; se ha envio pendente, confere o resultado na OLX.</summary>
        Task<OlxVeiculoInfo> ObterVeiculoAsync(int veiculoId, CancellationToken ct = default);

        /// <summary>Salva os codigos e envia o anuncio (inclui ou altera).</summary>
        Task<OlxVeiculoInfo> PublicarAsync(int veiculoId, OlxAnuncioDados dados, CancellationToken ct = default);

        /// <summary>Tira do ar pelo botao: so volta clicando em Anunciar.</summary>
        Task<OlxVeiculoInfo> PausarAsync(int veiculoId, CancellationToken ct = default);

        /// <summary>
        /// Acerta o anuncio com o estado do veiculo: reenvia se mudou e continua
        /// disponivel, tira do ar se saiu, volta se voltou. Nada para veiculo
        /// nunca anunciado ou pausado pelo usuario.
        /// </summary>
        Task SincronizarVeiculoAsync(int veiculoId);

        /// <summary>Fotos mudaram: espera um pouco, junta pedidos seguidos e sincroniza.</summary>
        Task FotosMudaramAsync(int veiculoId);
    }
}
