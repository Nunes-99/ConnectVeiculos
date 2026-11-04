using ConnectVeiculos.Core.Contratos;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>Modelos de contrato, contratos dos veiculos e a assinatura eletronica.</summary>
    public interface IContratoService
    {
        // Modelos
        Task<IReadOnlyList<ModeloContratoInfo>> ListarModelosAsync(bool incluirInativos, CancellationToken ct = default);
        Task<ModeloContratoInfo> SalvarModeloAsync(int? id, ModeloContratoInput input, CancellationToken ct = default);
        Task ExcluirModeloAsync(int id, CancellationToken ct = default);
        IReadOnlyList<ContratoVariavel> Variaveis();
        Task<ContratoPreview> PreviewModeloAsync(ModeloPreviewInput input, CancellationToken ct = default);

        // Contratos
        Task<ContratoSugestao> SugerirAsync(int veiculoId, int modeloId, CancellationToken ct = default);
        Task<ContratoPreview> PreviewAsync(ContratoGerarInput input, CancellationToken ct = default);
        Task<ContratoInfo> GerarAsync(ContratoGerarInput input, int? usuarioId, CancellationToken ct = default);
        Task<IReadOnlyList<ContratoInfo>> ListarAsync(ContratoFiltro filtro, CancellationToken ct = default);
        Task<ContratoInfo> ObterAsync(int id, CancellationToken ct = default);
        Task<ContratoPreview> TextoAsync(int id, CancellationToken ct = default);
        Task<ContratoInfo> CancelarAsync(int id, CancellationToken ct = default);
        Task<ContratoInfo> AssinarPelaLojaAsync(int id, AssinaturaInput input, int usuarioId, string usuarioNome, AssinaturaOrigem origem, CancellationToken ct = default);
        Task<ContratoInfo> AssinarPresencialAsync(int id, AssinaturaInput input, int usuarioId, string usuarioNome, AssinaturaOrigem origem, CancellationToken ct = default);
        Task EnviarLinkPorEmailAsync(int id, string urlBase, CancellationToken ct = default);
        Task<(byte[] Pdf, string NomeArquivo)> PdfAsync(int id, CancellationToken ct = default);

        // Link publico (sem login)
        Task<AssinaturaPublicaInfo> AbrirLinkAsync(string token, CancellationToken ct = default);
        Task<string> EnviarCodigoAsync(string token, CancellationToken ct = default);
        Task<AssinaturaPublicaInfo> AssinarPeloLinkAsync(string token, AssinaturaInput input, AssinaturaOrigem origem, CancellationToken ct = default);
        Task<(byte[] Pdf, string NomeArquivo)> PdfPeloLinkAsync(string token, CancellationToken ct = default);
    }
}
