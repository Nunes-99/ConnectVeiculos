using ConnectVeiculos.Core.Napista;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Anuncios e leads da loja no NaPista. A loja conecta a conta dela por
    /// OAuth (o mesmo usuario e senha do NaPista); o ConnectVeiculos e' o
    /// integrador de estoque, com client_id proprio e sem segredo.
    /// Erros que a tela mostra chegam como <see cref="NapistaException"/>.
    /// </summary>
    public interface INapistaService
    {
        Task<NapistaStatus> StatusAsync(CancellationToken ct = default);
        string UrlAutorizacao();
        Task<NapistaStatus> ConectarAsync(string code, string? state, CancellationToken ct = default);
        Task<NapistaStatus> EscolherLojaAsync(string sellerId, CancellationToken ct = default);
        Task DesconectarAsync(CancellationToken ct = default);

        // Catalogo do NaPista (igual para todas as lojas).
        Task<IReadOnlyList<NapistaItem>> MarcasAsync(string categoria, CancellationToken ct = default);
        Task<IReadOnlyList<NapistaItem>> ModelosAsync(string categoria, string marcaId, int? ano, CancellationToken ct = default);
        Task<IReadOnlyList<NapistaItem>> VersoesAsync(string categoria, string modeloId, int? ano, CancellationToken ct = default);
        Task<NapistaAtributos> AtributosAsync(string categoria, CancellationToken ct = default);

        Task<NapistaVeiculoInfo> ObterVeiculoAsync(int veiculoId, CancellationToken ct = default);

        /// <summary>Salva a versao/valores e cria (ou atualiza) o anuncio, com fotos.</summary>
        Task<NapistaVeiculoInfo> PublicarAsync(int veiculoId, NapistaAnuncioDados dados, CancellationToken ct = default);

        /// <summary>Tira do ar pelo botao (UNPUBLISHED): so volta clicando em Anunciar.</summary>
        Task<NapistaVeiculoInfo> PausarAsync(int veiculoId, CancellationToken ct = default);

        /// <summary>
        /// Acerta o anuncio com o estado do veiculo: atualiza se continua
        /// disponivel, marca vendido/tira do ar se saiu, republica se voltou.
        /// Nao faz nada para veiculo nunca anunciado ou pausado pelo usuario.
        /// </summary>
        Task SincronizarVeiculoAsync(int veiculoId);

        /// <summary>
        /// Fotos do veiculo mudaram (upload, exclusao, principal): manda a lista
        /// nova para o anuncio no ar. Espera um pouco e junta pedidos seguidos.
        /// </summary>
        Task FotosMudaramAsync(int veiculoId);

        /// <summary>Anuncios publicados da loja no NaPista, marcando os que ja estao no ConnectVeiculos.</summary>
        Task<List<NapistaAnuncioExistente>> ListarAnunciosExistentesAsync(CancellationToken ct = default);

        /// <summary>
        /// Cria os veiculos (com fotos) a partir de anuncios que ja estao no
        /// NaPista e liga cada um ao anuncio, sem criar outro la. Se ja existe
        /// veiculo com a mesma placa, so liga.
        /// </summary>
        Task<NapistaImportacaoResultado> ImportarAnunciosAsync(NapistaImportarPedido pedido, CancellationToken ct = default);

        /// <summary>Traz os leads do NaPista para a lista de Leads (sem duplicar).</summary>
        Task<NapistaLeadsResultado> ImportarLeadsAsync(int dias, CancellationToken ct = default);
    }
}
