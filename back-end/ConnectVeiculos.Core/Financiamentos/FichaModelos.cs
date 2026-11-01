namespace ConnectVeiculos.Core.Financiamentos
{
    /// <summary>Recusa de regra da ficha/bancos, com texto para a tela.</summary>
    public class FinanciamentoException : Exception
    {
        public FinanciamentoException(string mensagem) : base(mensagem) { }
    }

    /// <summary>Taxa mensal (%) de um prazo.</summary>
    public class TaxaPrazo
    {
        public int Meses { get; set; }
        public decimal TaxaMensal { get; set; }
    }

    /// <summary>
    /// Uma linha da tabela do banco: vale para veiculos de AnoDe a AnoAte
    /// (vazio = sem limite) com entrada de pelo menos EntradaMinimaPct.
    /// </summary>
    public class FaixaTaxaBanco
    {
        public short? AnoDe { get; set; }
        public short? AnoAte { get; set; }
        public decimal EntradaMinimaPct { get; set; }
        public List<TaxaPrazo> Taxas { get; set; } = new();
    }

    public class BancoLojaInfo
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? PortalUrl { get; set; }
        public bool Ativo { get; set; } = true;
        public decimal TarifaCadastro { get; set; }
        public decimal RegistroContrato { get; set; }
        public bool TarifasFinanciadas { get; set; } = true;
        public List<FaixaTaxaBanco> Faixas { get; set; } = new();
        public string? Observacao { get; set; }
    }

    public class EstimativaInput
    {
        public decimal ValorVeiculo { get; set; }
        public decimal Entrada { get; set; }
        public short AnoVeiculo { get; set; }
    }

    /// <summary>Parcela estimada de um prazo em um banco.</summary>
    public class EstimativaPrazo
    {
        public int Meses { get; set; }
        public decimal TaxaMensal { get; set; }
        public decimal ValorParcela { get; set; }
        public decimal ValorFinanciado { get; set; }
        public decimal Iof { get; set; }
        public decimal Tarifas { get; set; }
        public decimal TotalPago { get; set; }
        public decimal CetMensal { get; set; }
        public decimal CetAnual { get; set; }
    }

    public class EstimativaBanco
    {
        public int BancoId { get; set; }
        public string Banco { get; set; } = string.Empty;
        public string? PortalUrl { get; set; }

        /// <summary>Por que o banco nao entrou (ano fora da tabela, entrada baixa...), ou null.</summary>
        public string? Motivo { get; set; }
        public List<EstimativaPrazo> Prazos { get; set; } = new();
    }

    public static class StatusProposta
    {
        public const string Enviada = "ENVIADA";
        public const string EmAnalise = "EM_ANALISE";
        public const string Pendencia = "PENDENCIA";
        public const string Aprovada = "APROVADA";
        public const string Recusada = "RECUSADA";

        public static readonly string[] Todos = { Enviada, EmAnalise, Pendencia, Aprovada, Recusada };
    }

    /// <summary>Retorno de um banco para a ficha, anotado pelo vendedor.</summary>
    public class PropostaBanco
    {
        public int BancoId { get; set; }
        public string Banco { get; set; } = string.Empty;
        public string Status { get; set; } = StatusProposta.Enviada;
        public int? Meses { get; set; }
        public decimal? TaxaMensal { get; set; }
        public decimal? ValorParcela { get; set; }
        public decimal? EntradaExigida { get; set; }
        public string? Observacao { get; set; }
        public DateTime AtualizadoEm { get; set; }
    }

    public class FichaInput
    {
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int? VeiculoId { get; set; }
        public decimal ValorVeiculo { get; set; }
        public decimal Entrada { get; set; }
        public short AnoVeiculo { get; set; }
        public string? Observacao { get; set; }

        /// <summary>O cliente autorizou a consulta de credito (obrigatorio para criar).</summary>
        public bool Consentimento { get; set; }
    }

    public class FichaInfo
    {
        public int Id { get; set; }
        public int? VeiculoId { get; set; }
        public string? Veiculo { get; set; }
        public int VendedorId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal ValorVeiculo { get; set; }
        public decimal Entrada { get; set; }
        public short AnoVeiculo { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Observacao { get; set; }
        public DateTime ConsentimentoEm { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime AtualizadoEm { get; set; }
        public List<PropostaBanco> Propostas { get; set; } = new();
    }
}
