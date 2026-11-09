namespace ConnectVeiculos.Core.Interfaces.Email
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body);

        /// <summary>Envia com anexos (ex.: XML e DANFE da NF-e). False se nao deu.</summary>
        Task<bool> SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAnexo> anexos);
        Task<bool> SendVendaConfirmadaAsync(string to, string compradorNome, string veiculoDescricao, decimal valorVenda);
        Task<bool> SendVendaEstornadaAsync(string to, string compradorNome, string veiculoDescricao);
        Task<bool> SendNovoUsuarioAsync(string to, string usuarioNome, string senhaTemporaria);
        Task<bool> SendRecuperacaoSenhaAsync(string to, string usuarioNome, string token);
        Task<bool> SendPrecoAlteradoAsync(string to, string nome, string veiculoDesc, decimal precoAntigo, decimal precoNovo, string linkCatalogo);
        Task<bool> SendVeiculoSimilarAsync(string to, string nome, string veiculoDesc, decimal preco, string linkCatalogo);

        // Configuracao via UI/banco
        Task<EmailConfigInfo> GetConfigAsync();
        Task SalvarConfigAsync(EmailConfigInput input);
        Task DesconectarAsync();
        Task<EmailTestResult> TestarEnvioAsync(string destinatario);
    }

    public class EmailConfigInfo
    {
        public bool Configurado { get; set; }
        public string? SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string? SenderEmail { get; set; }
        public string? SenderName { get; set; }
        public string? Username { get; set; }
        public bool EnableSsl { get; set; }
    }

    public class EmailConfigInput
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SenderEmail { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
    }

    public class EmailTestResult
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
    }

    /// <summary>Arquivo anexado a um e-mail.</summary>
    public record EmailAnexo(string Nome, byte[] Conteudo, string ContentType);
}
