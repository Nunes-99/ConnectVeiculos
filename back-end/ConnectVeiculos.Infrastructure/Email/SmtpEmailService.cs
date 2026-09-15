using System.Globalization;
using System.Net;
using System.Text;
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
        private readonly Core.Interfaces.Database.Repositories.Lojas.ILojaRepository _lojaRepository;
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
            Core.Interfaces.Database.Repositories.Lojas.ILojaRepository lojaRepository,
            ILogger<SmtpEmailService> logger)
        {
            _settings = settings.Value;
            _configRepository = configRepository;
            _lojaRepository = lojaRepository;
            _logger = logger;
        }

        /// <summary>
        /// Endereco publico do sistema, tirado do catalogo da loja — e a unica
        /// URL que o tenant conhece. O template de recuperacao tinha um link
        /// fixo pra connectveiculos.com.br, dominio que nao e o nosso, e que
        /// nem chegava a ser usado no corpo do e-mail.
        /// </summary>
        private async Task<string> ResolverUrlBaseAsync()
        {
            try
            {
                var loja = (await _lojaRepository.GetAllAsync())
                    .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.LojUrlCatalogo));

                if (loja?.LojUrlCatalogo is not string url) return "";
                if (!Uri.TryCreate(url.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)) return "";

                return $"{uri.Scheme}://{uri.Authority}";
            }
            catch { return ""; }
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

                // UTF-8 explicito no assunto e no corpo. Sem isto o .NET escolhe a
                // codificacao sozinho e acento vira caractere quebrado dependendo do
                // cliente de e-mail — motivo pelo qual os textos do sistema vinham
                // todos sem acento.
                var message = new MailMessage
                {
                    From = new MailAddress(s.SenderEmail, s.SenderName),
                    Subject = subject,
                    SubjectEncoding = Encoding.UTF8,
                    Body = body,
                    BodyEncoding = Encoding.UTF8,
                    HeadersEncoding = Encoding.UTF8,
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
                return new EmailTestResult { Sucesso = false, Mensagem = "Informe um destinatário." };

            try
            {
                var s = await ResolveSettingsAsync();
                if (string.IsNullOrEmpty(s.SmtpServer) || string.IsNullOrEmpty(s.SenderEmail))
                    return new EmailTestResult { Sucesso = false, Mensagem = "SMTP não configurado. Preencha os campos e salve antes de testar." };

                var conteudoTeste = @"
    <p style='margin:0 0 16px'>Este é um e-mail de teste enviado pelo painel de Integrações.</p>
    <p style='margin:0 0 20px'>Se você está vendo esta mensagem, a configuração está <strong>correta</strong>:
    o sistema consegue enviar avisos de venda, recuperação de senha e notificações de
    queda de preço para quem favoritou um veículo.</p>";

                var ok = await SendEmailAsync(destinatario,
                    "Teste de configuração de e-mail - ConnectVeículos",
                    MontarEmail("Envio funcionando", Verde, conteudoTeste));

                return new EmailTestResult
                {
                    Sucesso = ok,
                    Mensagem = ok ? "E-mail de teste enviado. Verifique a caixa de entrada e a pasta de spam." : "Falha no envio. Confira servidor, porta, usuário e senha."
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
            var subject = "Venda confirmada - ConnectVeículos";
            var body = GetVendaConfirmadaTemplate(compradorNome, veiculoDescricao, valorVenda);
            return await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendVendaEstornadaAsync(string to, string compradorNome, string veiculoDescricao)
        {
            var subject = "Venda estornada - ConnectVeículos";
            var body = GetVendaEstornadaTemplate(compradorNome, veiculoDescricao);
            return await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendNovoUsuarioAsync(string to, string usuarioNome, string senhaTemporaria)
        {
            var subject = "Sua conta no ConnectVeículos está pronta";
            var body = GetNovoUsuarioTemplate(usuarioNome, senhaTemporaria);
            return await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendRecuperacaoSenhaAsync(string to, string usuarioNome, string token)
        {
            var subject = "Redefinição de senha - ConnectVeículos";
            var body = GetRecuperacaoSenhaTemplate(usuarioNome, token, await ResolverUrlBaseAsync());
            return await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendPrecoAlteradoAsync(string to, string nome, string veiculoDesc, decimal precoAntigo, decimal precoNovo, string linkCatalogo)
        {
            var queda = precoAntigo - precoNovo;
            var pct = precoAntigo > 0 ? Math.Round(queda / precoAntigo * 100, 1) : 0;

            var conteudo = $@"
    <p style='margin:0 0 16px'>Olá{(string.IsNullOrEmpty(nome) ? "" : " <strong>" + E(nome) + "</strong>")},</p>
    <p style='margin:0 0 20px'>O veículo que você favoritou ficou mais barato.</p>
    {Destaque($@"<strong style='display:block;margin-bottom:6px'>{E(veiculoDesc)}</strong>
      <span style='text-decoration:line-through;color:#6b7280'>{Moeda(precoAntigo)}</span>
      &nbsp;<span style='font-size:20px;color:{Verde};font-weight:bold'>{Moeda(precoNovo)}</span><br>
      <span style='font-size:13px;color:#6b7280'>Economia de {Moeda(queda)} ({Percentual(pct)}%)</span>", Verde, "#dcfce7")}
    {Botao(linkCatalogo, "Ver veículo")}";

            var body = MontarEmail("O preço baixou", Verde, conteudo,
                "Você está recebendo este e-mail porque favoritou este veículo.");

            return await SendEmailAsync(to, $"O preço baixou: {veiculoDesc} por {Moeda(precoNovo)}", body);
        }

        public async Task<bool> SendVeiculoSimilarAsync(string to, string nome, string veiculoDesc, decimal preco, string linkCatalogo)
        {
            var conteudo = $@"
    <p style='margin:0 0 16px'>Olá{(string.IsNullOrEmpty(nome) ? "" : " <strong>" + E(nome) + "</strong>")},</p>
    <p style='margin:0 0 20px'>Acabou de chegar um veículo parecido com os que você favoritou.</p>
    {Destaque($@"<strong style='display:block;margin-bottom:6px'>{E(veiculoDesc)}</strong>
      <span style='font-size:20px;color:{AzulMarca};font-weight:bold'>{Moeda(preco)}</span>", AzulMarca, "#eef2ff")}
    {Botao(linkCatalogo, "Ver no catálogo")}";

            var body = MontarEmail("Novo veículo no estoque", AzulMarca, conteudo,
                "Você está recebendo este e-mail porque favoritou veículos parecidos.");

            return await SendEmailAsync(to, $"Chegou um veículo parecido: {veiculoDesc}", body);
        }

        // ==========================================================
        // MOLDE DOS E-MAILS
        // ==========================================================

        private const string AzulMarca = "#1a237e";
        private const string Verde = "#16a34a";
        private const string Ambar = "#b45309";

        /// <summary>
        /// Casca comum a todos os e-mails: cabecalho colorido, corpo em cartao
        /// branco e rodape. Cada template so escreve o proprio conteudo.
        ///
        /// Antes cada um repetia o seu <style> e a sua estrutura, com resultados
        /// diferentes entre si — e nenhum tinha acento, porque o MailMessage ia
        /// sem BodyEncoding e a acentuacao quebrava dependendo do cliente.
        /// </summary>
        private static string MontarEmail(string titulo, string corCabecalho, string conteudo, string? rodapeExtra = null)
        {
            var rodape = rodapeExtra ?? "Mensagem automática. Por favor, não responda a este e-mail.";

            return $@"<!DOCTYPE html><html><body style='margin:0;padding:0;background:#f3f4f6;font-family:Arial,Helvetica,sans-serif;line-height:1.6;color:#333'>
<div style='max-width:600px;margin:0 auto;padding:20px'>
  <div style='background:{corCabecalho};color:#fff;padding:20px;text-align:center;border-radius:8px 8px 0 0'>
    <h1 style='margin:0;font-size:20px'>{titulo}</h1>
  </div>
  <div style='padding:24px;background:#fff;border:1px solid #e5e7eb;border-top:0;border-radius:0 0 8px 8px'>
    {conteudo}
    <p style='margin:0;padding-top:16px;border-top:1px solid #e5e7eb;color:#6b7280;font-size:13px'>{rodape}</p>
  </div>
  <p style='text-align:center;color:#9ca3af;font-size:12px;margin:16px 0 0'>ConnectVeículos</p>
</div>
</body></html>";
        }

        /// <summary>Bloco em destaque, para dados do veiculo ou avisos.</summary>
        private static string Destaque(string conteudo, string cor, string fundo) =>
            $@"<div style='background:{fundo};border-left:4px solid {cor};padding:14px 16px;border-radius:6px;margin:0 0 20px'>{conteudo}</div>";

        private static string Botao(string url, string texto) =>
            $@"<p style='margin:0 0 20px'><a href='{url}' style='display:inline-block;background:{AzulMarca};color:#fff;padding:14px 30px;border-radius:8px;text-decoration:none;font-weight:600'>{texto}</a></p>";

        private static string E(string? texto) => System.Net.WebUtility.HtmlEncode(texto ?? "");

        /// <summary>
        /// Valor em reais. O container roda sem cultura definida, entao ":C"
        /// formatava com a cultura invariante e o cliente recebia "¤119,000.00"
        /// no lugar de "R$ 119.000,00" — em todo e-mail que mostra dinheiro.
        /// </summary>
        private static string Moeda(decimal valor) =>
            valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

        // Pela mesma razao do Moeda(): sem LANG no container a cultura e a
        // invariante, e a porcentagem saia com ponto decimal ("5.9%").
        private static string Percentual(decimal valor) =>
            valor.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"));

        private static string GetVendaConfirmadaTemplate(string compradorNome, string veiculoDescricao, decimal valorVenda)
        {
            var conteudo = $@"
    <p style='margin:0 0 16px'>Olá <strong>{E(compradorNome)}</strong>,</p>
    <p style='margin:0 0 20px'>Confirmamos a venda do seu veículo. Obrigado pela preferência!</p>
    {Destaque($@"<strong style='display:block;margin-bottom:4px'>{E(veiculoDescricao)}</strong>Valor: <strong>{Moeda(valorVenda)}</strong>", Verde, "#dcfce7")}
    <p style='margin:0 0 20px'>Em breve entraremos em contato para os próximos passos.</p>";

            return MontarEmail("Venda confirmada", Verde, conteudo);
        }

        private static string GetVendaEstornadaTemplate(string compradorNome, string veiculoDescricao)
        {
            var conteudo = $@"
    <p style='margin:0 0 16px'>Olá <strong>{E(compradorNome)}</strong>,</p>
    <p style='margin:0 0 20px'>A venda do veículo abaixo foi estornada.</p>
    {Destaque($"<strong>{E(veiculoDescricao)}</strong>", Ambar, "#fef3c7")}
    <p style='margin:0 0 20px'>Se tiver qualquer dúvida sobre o estorno, entre em contato conosco.</p>";

            return MontarEmail("Venda estornada", Ambar, conteudo);
        }

        private static string GetNovoUsuarioTemplate(string usuarioNome, string senhaTemporaria)
        {
            var conteudo = $@"
    <p style='margin:0 0 16px'>Olá <strong>{E(usuarioNome)}</strong>,</p>
    <p style='margin:0 0 20px'>Sua conta foi criada no ConnectVeículos. Use a senha temporária
    abaixo para entrar pela primeira vez.</p>
    <div style='background:#eef2ff;padding:14px;border-radius:6px;margin:0 0 20px;font-family:monospace;font-size:15px'>{E(senhaTemporaria)}</div>
    {Destaque("<strong style='display:block;margin-bottom:4px'>Troque a senha no primeiro acesso</strong>Enquanto ela estiver valendo, qualquer pessoa com este e-mail consegue entrar na sua conta.", Ambar, "#fef3c7")}";

            return MontarEmail("Bem-vindo ao ConnectVeículos", AzulMarca, conteudo);
        }

        private static string GetRecuperacaoSenhaTemplate(string usuarioNome, string token, string urlBase)
        {
            // O e-mail mandava o token cru e pedia pro usuario "utilizar o codigo",
            // sem dizer onde. A tela /redefinir-senha ja aceita ?token=, entao o
            // link leva direto pros campos de senha nova e confirmacao.
            var linkRedefinir = string.IsNullOrEmpty(urlBase)
                ? ""
                : $"{urlBase}/redefinir-senha?token={Uri.EscapeDataString(token)}";

            // Sem link montado (loja sem URL de catalogo), o codigo volta a ser a
            // saida — melhor que um e-mail sem acao nenhuma.
            var acao = string.IsNullOrEmpty(linkRedefinir)
                ? $@"<p style='margin:0 0 8px'>Use o código abaixo na tela de redefinição de senha:</p>
    <div style='background:#eef2ff;padding:14px;border-radius:6px;margin:0 0 20px;font-family:monospace;word-break:break-all;font-size:13px'>{token}</div>"
                : Botao(linkRedefinir, "Criar nova senha") + $@"<p style='margin:0 0 20px;font-size:13px;color:#6b7280'>
      Se o botão não funcionar, copie e cole este endereço no navegador:<br>
      <span style='word-break:break-all'>{linkRedefinir}</span>
    </p>";

            var conteudo = $@"
    <p style='margin:0 0 16px'>Olá <strong>{E(usuarioNome)}</strong>,</p>
    <p style='margin:0 0 20px'>Recebemos um pedido para redefinir a senha da sua conta.
    Clique no botão abaixo para escolher uma nova.</p>
    {acao}
    {Destaque("<strong style='display:block;margin-bottom:4px'>O link vale por 2 horas</strong>Se você não pediu a redefinição, ignore este e-mail — sua senha continua a mesma.", Ambar, "#fef3c7")}";

            return MontarEmail("Redefinição de senha", AzulMarca, conteudo);
        }
    }
}
