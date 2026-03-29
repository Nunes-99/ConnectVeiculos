namespace ConnectVeiculos.Core.Entities.Leads
{
    public class Lead
    {
        public int LeaId { get; private set; }
        public int? R_VeiId { get; private set; }
        public int? R_LojId { get; private set; }
        public string LeaNomeCliente { get; private set; }
        public string LeaTelefone { get; private set; }
        public string LeaEmail { get; private set; }
        public string LeaOrigem { get; private set; } // WHATSAPP_CATALOGO, WHATSAPP_DETALHE, TEST_DRIVE, DIRETO, INDICACAO
        public string LeaStatus { get; private set; } // NOVO, CONTATO, NEGOCIANDO, CONVERTIDO, PERDIDO
        public string LeaObservacao { get; private set; }
        public DateTime LeaDtCriacao { get; private set; }

        public Lead() { }

        public Lead(int leaId, int? rVeiId, int? rLojId, string leaNomeCliente, string leaTelefone,
            string leaEmail, string leaOrigem, string leaStatus, string leaObservacao)
        {
            LeaId = leaId;
            R_VeiId = rVeiId;
            R_LojId = rLojId;
            LeaNomeCliente = leaNomeCliente;
            LeaTelefone = leaTelefone;
            LeaEmail = leaEmail;
            LeaOrigem = leaOrigem;
            LeaStatus = string.IsNullOrEmpty(leaStatus) ? "NOVO" : leaStatus;
            LeaObservacao = leaObservacao;
            LeaDtCriacao = DateTime.Now;
        }

        public void AlterarStatus(string novoStatus) { LeaStatus = novoStatus; }
    }
}
