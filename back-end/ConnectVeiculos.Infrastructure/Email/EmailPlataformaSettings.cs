namespace ConnectVeiculos.Infrastructure.Email
{
    /// <summary>
    /// Remetente da plataforma, usado so nos e-mails da conta de quem usa o
    /// sistema (hoje: redefinicao de senha). Os e-mails para clientes da loja
    /// continuam saindo pela conta da propria loja.
    /// </summary>
    public class EmailPlataformaSettings : EmailSettings
    {
        /// <summary>Endereco do sistema, para o link do e-mail quando a loja nao tem site proprio.</summary>
        public string PublicSiteUrl { get; set; } = string.Empty;

        public bool Configurado => !string.IsNullOrWhiteSpace(SmtpServer) && !string.IsNullOrWhiteSpace(SenderEmail);
    }
}
