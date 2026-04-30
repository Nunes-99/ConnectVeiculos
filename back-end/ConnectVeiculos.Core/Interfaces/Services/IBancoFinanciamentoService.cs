namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Interface para integração com bancos reais de financiamento de veículos.
    /// Cada banco implementa esta interface separadamente.
    /// </summary>
    public interface IBancoFinanciamentoService
    {
        string NomeBanco { get; }
        string CodigoBanco { get; }
        bool IsConfigured();
        Task<BancoSimulacaoResultado> SimularAsync(BancoSimulacaoRequest request);
        Task<BancoPropostaResultado> EnviarPropostaAsync(BancoPropostaRequest request);
        Task<BancoPropostaStatus> ConsultarStatusAsync(string propostaExternaId);
    }

    public class BancoSimulacaoRequest
    {
        public decimal ValorVeiculo { get; set; }
        public decimal ValorEntrada { get; set; }
        public int Parcelas { get; set; }
        public short AnoVeiculo { get; set; }
        public string TipoVeiculo { get; set; } = "USADO";
        public string CpfCliente { get; set; }
        public decimal RendaMensal { get; set; }
    }

    public class BancoSimulacaoResultado
    {
        public string Banco { get; set; }
        public string CodigoBanco { get; set; }
        public bool Aprovado { get; set; }
        public string Mensagem { get; set; }
        public decimal TaxaMensal { get; set; }
        public decimal TaxaAnual { get; set; }
        public decimal ValorParcela { get; set; }
        public decimal ValorFinanciado { get; set; }
        public decimal ValorTotal { get; set; }
        public decimal CetAnual { get; set; }
        public int Parcelas { get; set; }
        public string SimulacaoId { get; set; }
    }

    public class BancoPropostaRequest
    {
        public string SimulacaoId { get; set; }
        public int VeiculoId { get; set; }
        public string Marca { get; set; }
        public string Modelo { get; set; }
        public short Ano { get; set; }
        public string Placa { get; set; }
        public string Chassi { get; set; }
        public int Km { get; set; }
        public decimal ValorVeiculo { get; set; }
        public decimal ValorEntrada { get; set; }
        public int Parcelas { get; set; }
        public string NomeCliente { get; set; }
        public string CpfCliente { get; set; }
        public string RgCliente { get; set; }
        public DateTime DataNascimento { get; set; }
        public string TelefoneCliente { get; set; }
        public string EmailCliente { get; set; }
        public string EnderecoCliente { get; set; }
        public decimal RendaMensal { get; set; }
    }

    public class BancoPropostaResultado
    {
        public string Banco { get; set; }
        public string PropostaExternaId { get; set; }
        public string Status { get; set; } // PENDENTE, APROVADA, RECUSADA, EM_ANALISE
        public string Mensagem { get; set; }
        public decimal? TaxaAprovada { get; set; }
        public decimal? ValorParcelaAprovada { get; set; }
        public int? ParcelasAprovadas { get; set; }
        public string UrlContrato { get; set; }
    }

    public class BancoPropostaStatus
    {
        public string PropostaExternaId { get; set; }
        public string Banco { get; set; }
        public string Status { get; set; }
        public string Mensagem { get; set; }
        public DateTime UltimaAtualizacao { get; set; }
    }
}
