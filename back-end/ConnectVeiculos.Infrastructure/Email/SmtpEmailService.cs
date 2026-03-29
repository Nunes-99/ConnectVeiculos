using System.Net;
using System.Net.Mail;
using ConnectVeiculos.Core.Interfaces.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.SmtpServer))
                {
                    _logger.LogWarning("Email nao enviado: servidor SMTP nao configurado");
                    return false;
                }

                using var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    EnableSsl = _settings.EnableSsl
                };

                var message = new MailMessage
                {
                    From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                message.To.Add(to);

                await client.SendMailAsync(message);
                _logger.LogInformation("Email enviado para {To}: {Subject}", to, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar email para {To}: {Subject}", to, subject);
                return false;
            }
        }

        public async Task<bool> SendVendaConfirmadaAsync(string to, string compradorNome, string veiculoDescricao, decimal valorVenda)
        {
            var subject = "ConnectVeiculos - Confirmacao de Venda";
            var body = GetVendaConfirmadaTemplate(compradorNome, veiculoDescricao, valorVenda);
            return await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendVendaEstornadaAsync(string to, string compradorNome, string veiculoDescricao)
        {
            var subject = "ConnectVeiculos - Venda Estornada";
            var body = GetVendaEstornadaTemplate(compradorNome, veiculoDescricao);
            return await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendNovoUsuarioAsync(string to, string usuarioNome, string senhaTemporaria)
        {
            var subject = "ConnectVeiculos - Bem-vindo ao Sistema";
            var body = GetNovoUsuarioTemplate(usuarioNome, senhaTemporaria);
            return await SendEmailAsync(to, subject, body);
        }

        private static string GetVendaConfirmadaTemplate(string compradorNome, string veiculoDescricao, decimal valorVenda)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #1a237e; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .highlight {{ background: #e8f5e9; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ padding: 15px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>ConnectVeiculos</h1>
        </div>
        <div class='content'>
            <h2>Venda Confirmada!</h2>
            <p>Prezado(a) <strong>{compradorNome}</strong>,</p>
            <p>E com grande satisfacao que confirmamos a venda do veiculo:</p>
            <div class='highlight'>
                <strong>Veiculo:</strong> {veiculoDescricao}<br>
                <strong>Valor:</strong> {valorVenda:C}
            </div>
            <p>Em breve entraremos em contato para os proximos passos.</p>
            <p>Agradecemos pela preferencia!</p>
        </div>
        <div class='footer'>
            <p>Este e-mail foi enviado automaticamente pelo sistema ConnectVeiculos.</p>
            <p>Por favor, nao responda a este e-mail.</p>
        </div>
    </div>
</body>
</html>";
        }

        private static string GetVendaEstornadaTemplate(string compradorNome, string veiculoDescricao)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #c62828; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .highlight {{ background: #ffebee; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ padding: 15px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>ConnectVeiculos</h1>
        </div>
        <div class='content'>
            <h2>Venda Estornada</h2>
            <p>Prezado(a) <strong>{compradorNome}</strong>,</p>
            <p>Informamos que a venda do veiculo abaixo foi estornada:</p>
            <div class='highlight'>
                <strong>Veiculo:</strong> {veiculoDescricao}
            </div>
            <p>Para mais informacoes, entre em contato conosco.</p>
        </div>
        <div class='footer'>
            <p>Este e-mail foi enviado automaticamente pelo sistema ConnectVeiculos.</p>
            <p>Por favor, nao responda a este e-mail.</p>
        </div>
    </div>
</body>
</html>";
        }

        private static string GetNovoUsuarioTemplate(string usuarioNome, string senhaTemporaria)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #1a237e; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .credentials {{ background: #e3f2fd; padding: 15px; border-radius: 5px; margin: 15px 0; font-family: monospace; }}
        .warning {{ background: #fff3e0; padding: 10px; border-radius: 5px; margin: 15px 0; color: #e65100; }}
        .footer {{ padding: 15px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>ConnectVeiculos</h1>
        </div>
        <div class='content'>
            <h2>Bem-vindo ao ConnectVeiculos!</h2>
            <p>Ola <strong>{usuarioNome}</strong>,</p>
            <p>Sua conta foi criada com sucesso no sistema ConnectVeiculos.</p>
            <p>Utilize a senha temporaria abaixo para acessar o sistema:</p>
            <div class='credentials'>
                <strong>Senha temporaria:</strong> {senhaTemporaria}
            </div>
            <div class='warning'>
                <strong>Importante:</strong> Por seguranca, recomendamos que altere sua senha no primeiro acesso.
            </div>
        </div>
        <div class='footer'>
            <p>Este e-mail foi enviado automaticamente pelo sistema ConnectVeiculos.</p>
            <p>Por favor, nao responda a este e-mail.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
