using ConnectVeiculos.Core.Renave;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Renave: entrada e saida de veiculos no estoque eletronico (Senatran/Serpro,
    /// Renave-WS). A loja autentica com o proprio certificado A1, ou uma
    /// integradora autentica com o dela e informa o CNPJ da loja no cabecalho:
    /// para este servico, so muda a configuracao.
    /// Erros de negocio do Renave chegam como <see cref="RenaveException"/>.
    /// </summary>
    public interface IRenaveService
    {
        Task<RenaveConfigInfo> ObterConfigAsync(CancellationToken ct = default);
        Task<RenaveConfigInfo> SalvarConfigAsync(RenaveConfigInput input, CancellationToken ct = default);
        Task<RenaveConfigInfo> SalvarCertificadoAsync(byte[] pfx, string senha, CancellationToken ct = default);
        Task<RenaveConfigInfo> RemoverCertificadoAsync(CancellationToken ct = default);
        Task<RenaveConexao> TestarConexaoAsync(CancellationToken ct = default);
        Task<IReadOnlyList<RenaveMunicipio>> BuscarMunicipiosAsync(string uf, string? nome, CancellationToken ct = default);

        Task<RenaveSituacao> ObterSituacaoAsync(int veiculoId, CancellationToken ct = default);
        Task<IReadOnlyDictionary<int, RenaveSituacao>> ObterSituacoesAsync(IEnumerable<int> veiculoIds, CancellationToken ct = default);
        Task<RenaveSituacao> AtualizarSituacaoAsync(int veiculoId, CancellationToken ct = default);

        Task<RenaveAptidao> ConsultarAptidaoAsync(int veiculoId, RenaveCrvInput crv, CancellationToken ct = default);
        Task<RenaveSituacao> SolicitarEntradaAsync(int veiculoId, RenaveEntradaInput input, CancellationToken ct = default);
        Task<RenaveSituacao> EnviarAssinaturaVendedorAsync(int veiculoId, string tipo, byte[] foto, CancellationToken ct = default);
        Task<RenaveSituacao> EnviarNotaFiscalAsync(int veiculoId, string evento, string chave, CancellationToken ct = default);
        Task<RenaveSituacao> SolicitarSaidaAsync(int veiculoId, RenaveSaidaInput input, CancellationToken ct = default);
        Task<(byte[] Pdf, string Numero)> ObterAtpvAsync(int veiculoId, CancellationToken ct = default);

        /// <summary>Termo de entrada (saida=false) ou de saida (saida=true) do estoque, em PDF.</summary>
        Task<(byte[] Pdf, string Numero)> ObterTermoAsync(int veiculoId, bool saida, CancellationToken ct = default);

        /// <summary>Traz o estoque que ja esta no Renave para o sistema, sem fazer nova entrada.</summary>
        Task<RenaveImportacaoResultado> ImportarEstoqueAsync(CancellationToken ct = default);

        Task<RenaveConsulta> ConsultarVeiculoAsync(string? placa, string? chassi, string? renavam, CancellationToken ct = default);

        // Zero km (vindo da montadora) e entregas indicadas pela montadora.
        Task<IReadOnlyList<RenaveZeroKmPendente>> ListarZeroKmPendentesAsync(CancellationToken ct = default);
        Task<RenaveSituacao> EntradaZeroKmAsync(RenaveEntradaZeroKmInput input, CancellationToken ct = default);
        Task RejeitarZeroKmAsync(RenaveRejeicaoZeroKmInput input, CancellationToken ct = default);
        Task<IReadOnlyList<RenaveEntrega>> ListarEntregasAsync(string? estado, CancellationToken ct = default);
        Task<RenaveEntrega> RealizarEntregaAsync(long idEntrega, CancellationToken ct = default);
        Task<RenaveEntrega> CancelarEntregaAsync(long idEntrega, CancellationToken ct = default);

        // So no ambiente de homologacao: veiculo ficticio do Serpro e a
        // transferencia simulada no Detran, para testar o fluxo sem carro real.
        Task<RenaveVeiculoTeste> CriarVeiculoTesteAsync(CancellationToken ct = default);
        Task<RenaveZeroKmTeste> CriarZeroKmTesteAsync(bool acabado, CancellationToken ct = default);
        Task<RenaveSituacao> SimularTransferenciaDetranAsync(int veiculoId, CancellationToken ct = default);
    }
}
