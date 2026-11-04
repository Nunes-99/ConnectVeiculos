namespace ConnectVeiculos.Core.Renave
{
    /// <summary>
    /// Recusa do Renave (ou falha de configuracao) com texto que pode ir para a
    /// tela: o Serpro devolve "mensagemParaUsuarioFinal" justamente para isso.
    /// </summary>
    public class RenaveException : Exception
    {
        public int? StatusHttp { get; }

        public RenaveException(string mensagem, int? statusHttp = null) : base(mensagem)
        {
            StatusHttp = statusHttp;
        }
    }

    public static class RenaveAmbiente
    {
        public const string Homologacao = "homologacao";
        public const string Producao = "producao";

        public static string Normalizar(string? valor)
            => string.Equals(valor, Producao, StringComparison.OrdinalIgnoreCase) ? Producao : Homologacao;
    }

    /// <summary>Configuracao do Renave da loja, sem nada secreto (certificado e senha nunca saem).</summary>
    public class RenaveConfigInfo
    {
        public string Ambiente { get; set; } = RenaveAmbiente.Homologacao;
        public string? OperadorCpf { get; set; }
        public string? OperadorNome { get; set; }
        public string? EmailLoja { get; set; }

        /// <summary>CNPJ enviado no cabecalho CNPJ-Estabelecimento (acesso por integradora). Vazio = certificado da propria loja.</summary>
        public string? CnpjEstabelecimento { get; set; }

        public bool CertificadoCadastrado { get; set; }
        public string? CertificadoTitular { get; set; }
        public DateTime? CertificadoValidade { get; set; }

        /// <summary>Pronto para operar: operador e e-mail informados e, em producao, certificado valido.</summary>
        public bool Pronto { get; set; }
    }

    public class RenaveConfigInput
    {
        public string? Ambiente { get; set; }
        public string? OperadorCpf { get; set; }
        public string? OperadorNome { get; set; }
        public string? EmailLoja { get; set; }
        public string? CnpjEstabelecimento { get; set; }
    }

    public class RenaveConexao
    {
        public string Ambiente { get; set; } = RenaveAmbiente.Homologacao;
        public string? Cnpj { get; set; }
        public string? Nome { get; set; }
        public string? Integradora { get; set; }
    }

    /// <summary>Dados do CRV (documento do veiculo) pedidos pelo Renave.</summary>
    public class RenaveCrvInput
    {
        /// <summary>AZUL, VERDE, BRANCO ou DIGITAL.</summary>
        public string TipoCrv { get; set; } = "DIGITAL";
        public string? NumeroCrv { get; set; }
    }

    public class RenaveAptidao
    {
        public bool Apto { get; set; }
        public List<string> Motivos { get; set; } = new();
        public bool ComunicacaoComDetranFalhou { get; set; }
        public bool ExistemDebitos { get; set; }
        public decimal? DebitoIpva { get; set; }
        public decimal? DebitoLicenciamento { get; set; }
        public decimal? DebitoMultas { get; set; }
        public decimal? DebitoDpvat { get; set; }
        public decimal? OutrosDebitos { get; set; }
        public List<string> Restricoes { get; set; } = new();
    }

    public class RenaveEntradaInput
    {
        public string TipoCrv { get; set; } = "DIGITAL";
        public string? NumeroCrv { get; set; }
        public string CodigoSegurancaCrv { get; set; } = string.Empty;

        /// <summary>CPF ou CNPJ do vendedor (dono atual no CRV).</summary>
        public string DocumentoVendedor { get; set; } = string.Empty;
        public string? EmailVendedor { get; set; }

        public DateTime DataCompra { get; set; }
        public decimal ValorCompra { get; set; }
        public int QuilometragemHodometro { get; set; }
        public DateTime? DataHoraMedicaoHodometro { get; set; }
    }

    public class RenaveSaidaInput
    {
        public string CodigoSegurancaCrv { get; set; } = string.Empty;
        public string? NumeroCrv { get; set; }

        public string DocumentoComprador { get; set; } = string.Empty;
        public string? NomeComprador { get; set; }
        public string? EmailComprador { get; set; }

        /// <summary>Codigo do municipio no Renave (GET /municipios).</summary>
        public int CodigoMunicipio { get; set; }
        public string? Cep { get; set; }
        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }

        public DateTime DataVenda { get; set; }
        public decimal ValorVenda { get; set; }
    }

    /// <summary>Como o vendedor assinou o ATPV na compra.</summary>
    public static class RenaveTipoAssinatura
    {
        /// <summary>Foto do CRV em papel (verso) assinado com firma reconhecida.</summary>
        public const string CrvPapel = "crv-papel";
        /// <summary>Foto do ATPVe impresso e assinado com firma reconhecida.</summary>
        public const string AtpveImpresso = "atpve-impresso";
    }

    public class RenaveSituacao
    {
        public int VeiculoId { get; set; }
        public long? IdEstoque { get; set; }
        public string? Estado { get; set; }

        public DateTime? DataEntrada { get; set; }
        public decimal? ValorCompra { get; set; }
        public DateTime? DataAssinaturaVendedor { get; set; }
        public string? ChaveNfEntrada { get; set; }

        public DateTime? DataSaida { get; set; }
        public decimal? ValorVenda { get; set; }
        public string? CompradorNome { get; set; }
        public string? ChaveNfSaida { get; set; }
        public string? NumeroAtpv { get; set; }

        public string? UltimoErro { get; set; }
        public DateTime? AtualizadoEm { get; set; }

        /// <summary>Proximo passo sugerido para a tela (texto curto).</summary>
        public string ProximoPasso { get; set; } = string.Empty;
    }

    public class RenaveMunicipio
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
    }

    public class RenaveVeiculoTeste
    {
        public string Placa { get; set; } = string.Empty;
        public string Renavam { get; set; } = string.Empty;
        public string Chassi { get; set; } = string.Empty;
        public string NumeroCrv { get; set; } = string.Empty;
        public string CodigoSegurancaCrv { get; set; } = string.Empty;
        public string DocumentoProprietario { get; set; } = string.Empty;
    }
}
