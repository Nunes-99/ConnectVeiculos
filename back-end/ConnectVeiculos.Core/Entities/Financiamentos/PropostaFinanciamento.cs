namespace ConnectVeiculos.Core.Entities.Financiamentos
{
    public class PropostaFinanciamento
    {
        public int PrfId { get; private set; }
        public int R_VeiId { get; private set; }
        public string PrfBanco { get; private set; }
        public string PrfPropostaExternaId { get; private set; }
        public string PrfStatus { get; private set; }
        public string PrfNomeCliente { get; private set; }
        public string PrfCpfCliente { get; private set; }
        public string PrfTelefoneCliente { get; private set; }
        public string PrfEmailCliente { get; private set; }
        public decimal PrfValorVeiculo { get; private set; }
        public decimal PrfValorEntrada { get; private set; }
        public int PrfParcelas { get; private set; }
        public decimal PrfTaxaMensal { get; private set; }
        public decimal PrfValorParcela { get; private set; }
        public decimal PrfRendaMensal { get; private set; }
        public string PrfMensagem { get; private set; }
        public DateTime PrfDtCriacao { get; private set; }
        public DateTime PrfDtAtualizacao { get; private set; }

        public PropostaFinanciamento() { }

        public PropostaFinanciamento(int veiculoId, string banco, string nomeCliente, string cpfCliente,
            string telefone, string email, decimal valorVeiculo, decimal valorEntrada,
            int parcelas, decimal rendaMensal)
        {
            R_VeiId = veiculoId;
            PrfBanco = banco;
            PrfStatus = "SIMULADO";
            PrfNomeCliente = nomeCliente;
            PrfCpfCliente = cpfCliente;
            PrfTelefoneCliente = telefone;
            PrfEmailCliente = email;
            PrfValorVeiculo = valorVeiculo;
            PrfValorEntrada = valorEntrada;
            PrfParcelas = parcelas;
            PrfRendaMensal = rendaMensal;
            PrfDtCriacao = DateTime.UtcNow;
            PrfDtAtualizacao = DateTime.UtcNow;
        }

        public void AtualizarProposta(string propostaExternaId, string status, decimal taxaMensal, decimal valorParcela, string mensagem)
        {
            PrfPropostaExternaId = propostaExternaId;
            PrfStatus = status;
            PrfTaxaMensal = taxaMensal;
            PrfValorParcela = valorParcela;
            PrfMensagem = mensagem;
            PrfDtAtualizacao = DateTime.UtcNow;
        }

        public void AtualizarStatus(string status, string mensagem)
        {
            PrfStatus = status;
            PrfMensagem = mensagem;
            PrfDtAtualizacao = DateTime.UtcNow;
        }
    }
}
