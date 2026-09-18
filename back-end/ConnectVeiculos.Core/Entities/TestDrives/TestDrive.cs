namespace ConnectVeiculos.Core.Entities.TestDrives
{
    public class TestDrive
    {
        public int TdrId { get; private set; }
        public int R_VeiId { get; private set; }
        public int? R_LojId { get; private set; }
        public string TdrNomeCliente { get; private set; }
        public string TdrTelefone { get; private set; }
        public string TdrWhatsApp { get; private set; }
        public string TdrEmail { get; private set; }
        public DateTime TdrDataAgendamento { get; private set; }
        public string TdrHorario { get; private set; }
        public string TdrObservacao { get; private set; }
        public string TdrStatus { get; private set; } // P=Pendente, C=Confirmado, R=Realizado, X=Cancelado
        public DateTime TdrDtCriacao { get; private set; }

        public TestDrive() { }

        public TestDrive(int tdrId, int rVeiId, int? rLojId, string tdrNomeCliente, string tdrTelefone,
            string tdrWhatsApp, string tdrEmail, DateTime tdrDataAgendamento, string tdrHorario, string tdrObservacao, string tdrStatus)
        {
            TdrId = tdrId;
            R_VeiId = rVeiId;
            R_LojId = rLojId;
            TdrNomeCliente = tdrNomeCliente;
            TdrTelefone = tdrTelefone;
            TdrWhatsApp = tdrWhatsApp;
            TdrEmail = tdrEmail;
            TdrDataAgendamento = tdrDataAgendamento;
            TdrHorario = tdrHorario;
            TdrObservacao = tdrObservacao;
            TdrStatus = string.IsNullOrEmpty(tdrStatus) ? "P" : tdrStatus;
            TdrDtCriacao = DateTime.Now;
        }

        public void AlterarStatus(string novoStatus) { TdrStatus = novoStatus; }

        /// <summary>
        /// Muda a data e o horario de um agendamento que continua valendo.
        ///
        /// Ate 18/09/2026 nao existia: quem precisasse mudar o horario tinha de
        /// cancelar e criar outro, o que perde o registro original e faz o
        /// cliente receber um cancelamento por uma remarcacao combinada com ele.
        ///
        /// O horario e opcional aqui pelo mesmo motivo que e opcional no
        /// agendamento — nem toda loja trabalha com hora marcada.
        /// </summary>
        public void Reagendar(DateTime novaData, string? novoHorario)
        {
            TdrDataAgendamento = novaData;
            TdrHorario = string.IsNullOrWhiteSpace(novoHorario) ? "" : novoHorario.Trim();
        }
    }
}
