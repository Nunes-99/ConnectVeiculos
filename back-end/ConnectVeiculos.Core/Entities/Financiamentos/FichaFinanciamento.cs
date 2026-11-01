namespace ConnectVeiculos.Core.Entities.Financiamentos
{
    /// <summary>
    /// Ficha unica do cliente para financiamento: os dados que os bancos pedem
    /// (nome, CPF, telefone, e-mail), o veiculo, a entrada e o retorno de cada
    /// banco em que a loja mandou a proposta. So e criada com a autorizacao do
    /// cliente para consultar o credito dele (LGPD).
    /// </summary>
    public class FichaFinanciamento
    {
        public const string Aberta = "ABERTA";
        public const string Aprovada = "APROVADA";
        public const string Vendida = "VENDIDA";
        public const string Perdida = "PERDIDA";

        public int FifId { get; private set; }
        public int? R_VeiId { get; private set; }

        /// <summary>Vendedor que abriu a ficha.</summary>
        public int R_UsuId { get; private set; }

        public string FifNome { get; private set; } = string.Empty;
        public string FifCpf { get; private set; } = string.Empty;
        public string FifTelefone { get; private set; } = string.Empty;
        public string FifEmail { get; private set; } = string.Empty;

        public decimal FifValorVeiculo { get; private set; }
        public decimal FifEntrada { get; private set; }
        public short FifAnoVeiculo { get; private set; }

        public string FifStatus { get; private set; } = Aberta;

        /// <summary>Quando o cliente autorizou a consulta de credito.</summary>
        public DateTime FifConsentimentoEm { get; private set; }

        /// <summary>Retorno de cada banco (JSON de PropostaBanco).</summary>
        public string FifPropostasJson { get; private set; } = "[]";
        public string? FifObservacao { get; private set; }
        public DateTime FifCriadoEm { get; private set; }
        public DateTime FifAtualizadoEm { get; private set; }

        public FichaFinanciamento() { }

        public FichaFinanciamento(int usuarioId, DateTime consentimentoEm)
        {
            R_UsuId = usuarioId;
            FifConsentimentoEm = consentimentoEm;
            FifCriadoEm = DateTime.UtcNow;
            FifAtualizadoEm = FifCriadoEm;
        }

        public void AtualizarCliente(string nome, string cpf, string telefone, string email)
        {
            FifNome = nome;
            FifCpf = cpf;
            FifTelefone = telefone;
            FifEmail = email;
            Tocar();
        }

        public void AtualizarNegocio(int? veiculoId, decimal valorVeiculo, decimal entrada, short anoVeiculo, string? observacao)
        {
            R_VeiId = veiculoId;
            FifValorVeiculo = valorVeiculo;
            FifEntrada = entrada;
            FifAnoVeiculo = anoVeiculo;
            FifObservacao = observacao;
            Tocar();
        }

        public void AtualizarPropostas(string propostasJson)
        {
            FifPropostasJson = propostasJson;
            Tocar();
        }

        public void AlterarStatus(string status)
        {
            FifStatus = status;
            Tocar();
        }

        private void Tocar() => FifAtualizadoEm = DateTime.UtcNow;
    }
}
