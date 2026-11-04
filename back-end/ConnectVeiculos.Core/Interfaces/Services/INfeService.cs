using ConnectVeiculos.Core.NotasFiscais;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// NF-e de veiculos emitida pelo proprio sistema, direto na Sefaz da UF da
    /// loja, assinada com o certificado A1 dela. Recusas e validacoes chegam
    /// como <see cref="NfeException"/>.
    /// </summary>
    public interface INfeService
    {
        Task<NfeConfigInfo> ObterConfigAsync(CancellationToken ct = default);
        Task<NfeConfigInfo> SalvarConfigAsync(NfeConfigInfo input, CancellationToken ct = default);
        Task<NfeStatusSefaz> ConsultarStatusSefazAsync(CancellationToken ct = default);
        Task<IReadOnlyList<NfeMunicipio>> BuscarMunicipiosAsync(string uf, string? nome, CancellationToken ct = default);

        Task<IReadOnlyList<NfeResumo>> ListarDoVeiculoAsync(int veiculoId, CancellationToken ct = default);
        Task<IReadOnlyList<NfeResumo>> ListarAsync(NfeFiltro filtro, CancellationToken ct = default);
        Task<NfeSugestao> SugerirAsync(int veiculoId, string tipo, CancellationToken ct = default);
        Task<NfeResumo> EmitirAsync(int veiculoId, NfeEmissaoInput input, CancellationToken ct = default);
        Task<NfeResumo> ReenviarAsync(int notaId, NfeEmissaoInput? correcao, CancellationToken ct = default);
        Task<NfeResumo> CancelarAsync(int notaId, string justificativa, CancellationToken ct = default);
        Task<(string Xml, string NomeArquivo)> ObterXmlAsync(int notaId, CancellationToken ct = default);
        Task<NfeResumo> CorrigirAsync(int notaId, string correcao, CancellationToken ct = default);
        Task<(string Xml, string NomeArquivo)> ObterXmlCartaCorrecaoAsync(int notaId, CancellationToken ct = default);
        Task<IReadOnlyList<NfeInutilizacaoInfo>> ListarInutilizacoesAsync(CancellationToken ct = default);
        Task<NfeInutilizacaoInfo> InutilizarAsync(NfeInutilizacaoInput input, CancellationToken ct = default);
        Task<(string Xml, string NomeArquivo)> ObterXmlInutilizacaoAsync(int id, CancellationToken ct = default);
        Task<(byte[] Pdf, string NomeArquivo)> ObterDanfeAsync(int notaId, CancellationToken ct = default);
    }
}
