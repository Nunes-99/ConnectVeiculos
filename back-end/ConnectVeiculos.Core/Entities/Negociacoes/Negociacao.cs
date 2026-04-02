namespace ConnectVeiculos.Core.Entities.Negociacoes
{
    public class Negociacao
    {
        public int NegId { get; private set; }
        public int R_VeiId { get; private set; }
        public int? R_LojId { get; private set; }
        public string NegNomeCliente { get; private set; }
        public string NegTelefone { get; private set; }
        public string NegEmail { get; private set; }
        public decimal NegValorProposta { get; private set; }
        public string NegStatus { get; private set; } // PROPOSTA, CONTRAPROPOSTA, ACEITA, RECUSADA, CANCELADA
        public string NegObservacao { get; private set; }
        public DateTime NegDtCriacao { get; private set; }

        public Negociacao() { }

        public Negociacao(int negId, int rVeiId, int? rLojId, string negNomeCliente, string negTelefone,
            string negEmail, decimal negValorProposta, string negStatus, string negObservacao)
        {
            NegId = negId;
            R_VeiId = rVeiId;
            R_LojId = rLojId;
            NegNomeCliente = negNomeCliente;
            NegTelefone = negTelefone;
            NegEmail = negEmail;
            NegValorProposta = negValorProposta;
            NegStatus = string.IsNullOrEmpty(negStatus) ? "PROPOSTA" : negStatus;
            NegObservacao = negObservacao;
            NegDtCriacao = DateTime.Now;
        }

        public void AlterarStatus(string novoStatus) { NegStatus = novoStatus; }

        public void AtualizarProposta(decimal valorProposta, string observacao)
        {
            NegValorProposta = valorProposta;
            NegObservacao = observacao;
        }
    }
}
