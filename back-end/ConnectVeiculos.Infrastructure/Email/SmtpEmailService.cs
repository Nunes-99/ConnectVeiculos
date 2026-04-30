using System.Net;
using System.Net.Mail;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly IConfiguracaoSistemaRepository _configRepository;
        private readonly ILogger<SmtpEmailService> _logger;

        // Chaves no ConfiguracaoSistema
        private const string KEY_SERVER = "SMTP_SERVER";
        private const string KEY_PORT = "SMTP_PORT";
        private const string KEY_USERNAME = "SMTP_USERNAME";
        private const string KEY_PASSWORD = "SMTP_PASSWORD";
        private const string KEY_SENDER_EMAIL = "SMTP_SENDER_EMAIL";
        private const string KEY_SENDER_NAME = "SMTP_SENDER_NAME";
        private const string KEY_ENABLE_SSL = "SMTP_ENABLE_SSL";

        public SmtpEmailService(
            IOptions<EmailSettings> settings,
            IConfiguracaoSistemaRepository configRepository,
            ILogger<SmtpEmailService> logger)
        {
            _settings = settings.Value;
            _configRepository = configRepository;
            _logger = logger;
        }

        // Precedencia: env var > banco > appsettings
        private async Task<EmailSettings> ResolveSettingsAsync()
        {
            var s = new EmailSettings
            {
                SmtpServer = await ResolveAsync("EMAIL_SMTP_SERVER", KEY_SERVER, _settings.SmtpServer),
                SmtpPort = ParsePort(await ResolveAsync("EMAIL_SMTP_PORT", KEY_PORT, _settings.SmtpPort.ToString()), _settings.SmtpPort),
                Username = await ResolveAsync("EMAIL_USERNAME", KEY_USERNAME, _settings.Username),
                Password = await ResolveAsync("EMAIL_PASSWORD", KEY_PASSWORD, _settings.Password),
                SenderEmail = await ResolveAsync("EMAIL_SENDER_EMAIL", KEY_SENDER_EMAIL, _settings.SenderEmail),
                SenderName = await ResolveAsync("EMAIL_SENDER_NAME", KEY_SENDER_NAME, _settings.SenderName),
                EnableSsl = ParseBool(await ResolveAsync("EMAIL_ENABLE_SSL", KEY_ENABLE_SSL, _settings.EnableSsl ? "true" : "false"), _settings.EnableSsl)
            };
            return s;
        }

        private async Task<string> ResolveAsync(string envVar, string dbKey, string fallback)
        {
            var fromEnv = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;
            var fromDb = await _configRepository.GetValorAsync(dbKey);
            if (!string.IsNullOrEmpty(fromDb)) return fromDb;
            return fallback ?? "";
        }

        private static int ParsePort(string value, int fallback) => int.TryParse(value, out var p) && p > 0 ? p : fallback;
        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var s = await ResolveSettingsAsync();
                if (string.IsNullOrEmpty(s.SmtpServer))
                {
                    _logger.LogWarning("Email nao enviado: servidor SMTP nao configurado");
                    return false;
                }

                using var client = new SmtpClient(s.SmtpServer, s.SmtpPort)
                {
                    Credentials = new NetworkCredential(s.Username, s.Password),
                    EnableSsl = s.EnableSsl
                };

                var message = new MailMessage
                {
                    From = new MailAddress(s.SenderEmail, s.SenderName),
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

        public async Task<EmailConfigInfo> GetConfigAsync()
        {
            var s = await ResolveSettingsAsync();
            return new EmailConfigInfo
            {
                Configurado = !string.IsNullOrEmpty(s.SmtpServer) && !string.IsNullOrEmpty(s.SenderEmail),
                SmtpServer = string.IsNullOrEmpty(s.SmtpServer) ? null : s.SmtpServer,
                SmtpPort = s.SmtpPort,
                SenderEmail = string.IsNullOrEmpty(s.SenderEmail) ? null : s.SenderEmail,
                SenderName = string.IsNullOrEmpty(s.SenderName) ? null : s.SenderName,
                Username = string.IsNullOrEmpty(s.Username) ? null : s.Username,
                EnableSsl = s.EnableSsl
            };
        }

        public async Task SalvarConfigAsync(EmailConfigInput input)
        {
            await _configRepository.SetValorAsync(KEY_SERVER, input.SmtpServer ?? "");
            await _configRepository.SetValorAsync(KEY_PORT, input.SmtpPort.ToString());
            await _configRepository.SetValorAsync(KEY_USERNAME, input.Username ?? "");
            // So salva senha se informada (permite editar resto sem reenviar senha)
            if (!string.IsNullOrEmpty(input.Password))
                await _configRepository.SetValorAsync(KEY_PASSWORD, input.Password);
            await _configRepository.SetValorAsync(KEY_SENDER_EMAIL, input.SenderEmail ?? "");
            await _configRepository.SetValorAsync(KEY_SENDER_NAME, input.SenderName ?? "");
            await _configRepository.SetValorAsync(KEY_ENABLE_SSL, input.EnableSsl ? "true" : "false");
            _logger.LogInformation("SMTP config salvo (servidor={Server}, porta={Port}, sender={Sender})", input.SmtpServer, input.SmtpPort, input.SenderEmail);
        }

        public async Task DesconectarAsync()
        {
            await _configRepository.SetValorAsync(KEY_SERVER, "");
            await _configRepository.SetValorAsync(KEY_PORT, "");
            await _configRepository.SetValorAsync(KEY_USERNAME, "");
            await _configRepository.SetValorAsync(KEY_PASSWORD, "");
            await _configRepository.SetValorAsync(KEY_SENDER_EMAIL, "");
            await _configRepository.SetValorAsync(KEY_SENDER_NAME, "");
            await _configRepository.SetValorAsync(KEY_ENABLE_SSL, "");
            _logger.LogInformation("SMTP desconectado.");
        }

        public async Task<EmailTestResult> TestarEnvioAsync(string destinatario)
        {
            if (string.IsNullOrWhiteSpace(destinatario))
                return new EmailTestResult { Sucesso = false, Mensagem = "Destinatario obrigatorio." };

            try
            {
                var s = await ResolveSettingsAsync();
                if (string.IsNullOrEmpty(s.SmtpServer) || string.IsNullOrEmpty(s.SenderEmail))
                    return new EmailTestResult { Sucesso = false, Mensagem = "SMTP nao configurado. Preencha os campos antes de testar." };

                var ok = await SendEmailAsync(destinatario,
                    "ConnectVeiculos - Teste de configuracao SMTP",
                    @"<!DOCTYPE html><html><body style='font-family:Arial,sans-serif;line-height:1.6;color:#333'>
<div style='max-width:600px;margin:0 auto;padding:20px'>
<div style='background:#16a34a;color:white;padding:20px;text-align:center;border-radius:8px 8px 0 0'>
<h1>SMTP funcionando!</h1></div>
<div style='padding:20px;background:#f9f9f9'>
<p>Este e um e-mail de teste enviado pelo painel de Integracoes do ConnectVeiculos.</p>
<p>Se voce esta vendo isso, sua configuracao SMTP esta <strong>correta</strong>: o sistema consegue enviar e-mails de notificacao para os clientes que favoritaram veiculos.</p>
</div></div></body></html>");

                return new EmailTestResult
                {
                    Sucesso = ok,
                    Mensagem = ok ? "E-mail de teste enviado. Verifique a caixa de entrada (e a pasta de spam)." : "Falha no envio. Verifique servidor, porta, usuario e senha."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro testando SMTP");
                return new EmailTestResult { Sucesso = false, Mensagem = $"Erro: {ex.Message}" };
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

        public async Task<bool> SendRecuperacaoSenhaAsync(string to, string usuarioNome, string token)
        {
            var subject = "ConnectVeiculos - Recuperacao de Senha";
            var body = GetRecuperacaoSenhaTemplate(usuarioNome, token);
            return await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendPrecoAlteradoAsync(string to, string nome, string veiculoDesc, decimal precoAntigo, decimal precoNovo, string linkCatalogo)
        {
            var queda = precoAntigo - precoNovo;
            var pct = precoAntigo > 0 ? Math.Round(queda / precoAntigo * 100, 1) : 0;
            var subject = $"O preco baixou! {veiculoDesc} agora por {precoNovo:C}";
            var body = $@"<!DOCTYPE html><html><body style='font-family:Arial,sans-serif;line-height:1.6;color:#333'>
<div style='max-width:600px;margin:0 auto;padding:20px'>
<div style='background:#16a34a;color:white;padding:20px;text-align:center;border-radius:8px 8px 0 0'><h1>O preco baixou!</h1></div>
<div style='padding:20px;background:#f9f9f9'>
<p>Ola{(string.IsNullOrEmpty(nome) ? "" : ", " + nome)}!</p>
<p>O veiculo que voce favoritou teve o preco reduzido:</p>
<div style='background:#d1fae5;padding:15px;border-radius:6px;margin:15px 0'>
<strong>{System.Net.WebUtility.HtmlEncode(veiculoDesc)}</strong><br>
<span style='text-decoration:line-through;color:#888'>{precoAntigo:C}</span>
&nbsp;&nbsp;<span style='font-size:20px;color:#16a34a;font-weight:bold'>{precoNovo:C}</span><br>
<small>Economia de {queda:C} ({pct}%)</small>
</div>
<a href='{linkCatalogo}' style='display:inline-block;background:#1a237e;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Ver veiculo no catalogo</a>
</div>
<div style='padding:15px;text-align:center;font-size:12px;color:#666'>Voce esta recebendo este e-mail porque favoritou este veiculo.</div>
</div></body></html>";
            return await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendVeiculoSimilarAsync(string to, string nome, string veiculoDesc, decimal preco, string linkCatalogo)
        {
            var subject = $"Novo veiculo similar disponivel: {veiculoDesc}";
            var body = $@"<!DOCTYPE html><html><body style='font-family:Arial,sans-serif;line-height:1.6;color:#333'>
<div style='max-width:600px;margin:0 auto;padding:20px'>
<div style='background:#1a237e;color:white;padding:20px;text-align:center;border-radius:8px 8px 0 0'><h1>Novo veiculo similar!</h1></div>
<div style='padding:20px;background:#f9f9f9'>
<p>Ola{(string.IsNullOrEmpty(nome) ? "" : ", " + nome)}!</p>
<p>Acabou de chegar um veiculo parecido com os que voce favoritou:</p>
<div style='background:#dbeafe;padding:15px;border-radius:6px;margin:15px 0'>
<strong>{System.Net.WebUtility.HtmlEncode(veiculoDesc)}</strong><br>
<span style='font-size:20px;color:#1a237e;font-weight:bold'>{preco:C}</span>
</div>
<a href='{linkCatalogo}' style='display:inline-block;background:#1a237e;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Ver no catalogo</a>
</div>
<div style='padding:15px;text-align:center;font-size:12px;color:#666'>Voce esta recebendo este e-mail porque favoritou veiculos similares.</div>
</div></body></html>";
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
        private static string GetRecuperacaoSenhaTemplate(string usuarioNome, string token)
        {
            var resetUrl = $"https://connectveiculos.com.br/redefinir-senha?token={token}";
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #1a237e; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .btn {{ display: inline-block; background: #1a237e; color: white !important; padding: 14px 30px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 16px; margin: 15px 0; }}
        .warning {{ background: #fff3e0; padding: 12px; border-radius: 5px; margin: 15px 0; color: #e65100; font-size: 13px; }}
        .token-box {{ background: #e3f2fd; padding: 12px; border-radius: 5px; margin: 15px 0; font-family: monospace; word-break: break-all; font-size: 13px; }}
        .footer {{ padding: 15px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>ConnectVeiculos</h1>
        </div>
        <div class='content'>
            <h2>Recuperacao de Senha</h2>
            <p>Ola <strong>{usuarioNome}</strong>,</p>
            <p>Recebemos uma solicitacao para redefinir a senha da sua conta.</p>
            <p>Utilize o codigo abaixo para redefinir sua senha:</p>
            <div class='token-box'>
                <strong>Codigo:</strong> {token}
            </div>
            <div class='warning'>
                <strong>Importante:</strong> Este codigo e valido por 2 horas. Se voce nao solicitou a recuperacao de senha, ignore este e-mail.
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
