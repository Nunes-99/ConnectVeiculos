namespace ConnectVeiculos.Core.NotasFiscais
{
    public class NfeException : Exception
    {
        public NfeException(string mensagem) : base(mensagem) { }
    }

    /// <summary>
    /// Regras fiscais de uma operacao (compra ou venda de veiculo). Os padroes
    /// sao so ponto de partida: quem define e' o contador da loja.
    /// </summary>
    public class NfeRegraOperacao
    {
        public string NaturezaOperacao { get; set; } = string.Empty;
        public string CfopInterno { get; set; } = string.Empty;
        public string CfopInterestadual { get; set; } = string.Empty;

        /// <summary>CSOSN (Simples) ou CST (regime normal) do ICMS.</summary>
        public string CstIcms { get; set; } = string.Empty;
        public decimal AliquotaIcms { get; set; }
        /// <summary>Reducao da base de calculo do ICMS, em % (ex.: 95 = base de 5% do valor).</summary>
        public decimal ReducaoBaseIcms { get; set; }

        public string CstPis { get; set; } = "49";
        public string CstCofins { get; set; } = "49";
    }

    /// <summary>Emitente (a loja) e padroes da NF-e. Nada secreto.</summary>
    public class NfeConfigInfo
    {
        public string Ambiente { get; set; } = "homologacao";

        public string? Cnpj { get; set; }
        public string? InscricaoEstadual { get; set; }
        public string? RazaoSocial { get; set; }
        public string? NomeFantasia { get; set; }
        /// <summary>1 Simples Nacional, 2 Simples excesso de sublimite, 3 Regime normal, 4 MEI.</summary>
        public int Crt { get; set; } = 1;

        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cep { get; set; }
        /// <summary>Codigo IBGE do municipio (7 digitos).</summary>
        public int? CodigoMunicipio { get; set; }
        public string? Municipio { get; set; }
        public string? Uf { get; set; }
        public string? Telefone { get; set; }

        public int Serie { get; set; } = 1;
        public int ProximoNumero { get; set; } = 1;

        /// <summary>NCM padrao de automovel; varia pela cilindrada, confirme com o contador.</summary>
        public string NcmPadrao { get; set; } = "87032310";

        public NfeRegraOperacao Compra { get; set; } = new();
        public NfeRegraOperacao Venda { get; set; } = new();

        /// <summary>Texto fixo nas informacoes complementares (ex.: optante pelo Simples).</summary>
        public string? InformacoesComplementares { get; set; }

        // Responsavel tecnico (grupo infRespTec), exigido por algumas Sefaz.
        public string? RespTecCnpj { get; set; }
        public string? RespTecContato { get; set; }
        public string? RespTecEmail { get; set; }
        public string? RespTecTelefone { get; set; }

        public bool CertificadoValido { get; set; }
        public string? CertificadoCnpj { get; set; }

        /// <summary>Emitente completo, certificado valido e do mesmo CNPJ.</summary>
        public bool Pronto { get; set; }
        public List<string> Pendencias { get; set; } = new();
    }

    public class NfeEndereco
    {
        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cep { get; set; }
        public int CodigoMunicipio { get; set; }
        public string? Municipio { get; set; }
        public string? Uf { get; set; }
    }

    /// <summary>Dados de uma nota de veiculo: a outra parte (vendedor na compra, comprador na venda) e os valores.</summary>
    public class NfeEmissaoInput
    {
        /// <summary>ENTRADA (compra) ou SAIDA (venda).</summary>
        public string Tipo { get; set; } = "SAIDA";

        public string Documento { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string? InscricaoEstadual { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public NfeEndereco Endereco { get; set; } = new();

        public decimal Valor { get; set; }
        public string? Ncm { get; set; }

        /// <summary>Codigo do meio de pagamento da NF-e (01 dinheiro, 03 cartao, 17 Pix, 90 sem pagamento, 99 outros...).</summary>
        public string MeioPagamento { get; set; } = "99";
        public string? InformacoesComplementares { get; set; }
    }

    public class NfeResumo
    {
        public int Id { get; set; }
        public int VeiculoId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Ambiente { get; set; } = string.Empty;
        public int Serie { get; set; }
        public int Numero { get; set; }
        public string? Chave { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? CStat { get; set; }
        public string? Motivo { get; set; }
        public string? Protocolo { get; set; }
        public DateTime DataEmissao { get; set; }
        public DateTime? DataAutorizacao { get; set; }
        public decimal Valor { get; set; }
        public string? DestinatarioNome { get; set; }
        public DateTime? DataCancelamento { get; set; }
        public bool PodeReenviar { get; set; }
        public bool PodeCancelar { get; set; }
    }

    public class NfeMunicipio
    {
        public int Codigo { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
    }

    public class NfeStatusSefaz
    {
        public int CStat { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string Ambiente { get; set; } = string.Empty;
    }
}
