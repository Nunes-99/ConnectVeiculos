using System.Net;
using System.Net.Sockets;
using System.Text;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// O e-mail sai sempre pela conta da loja (Integracoes -> E-mail); nao ha
    /// remetente da plataforma. Um servidor SMTP falso local guarda o que
    /// chegou para conferir o remetente.
    /// </summary>
    [Collection("VariaveisDeAmbienteEmail")]
    public class SmtpEmailServiceTests : IDisposable
    {
        private static readonly string[] Variaveis =
            { "EMAIL_SMTP_SERVER", "EMAIL_SMTP_PORT", "EMAIL_USERNAME", "EMAIL_PASSWORD", "EMAIL_SENDER_EMAIL", "EMAIL_SENDER_NAME", "EMAIL_ENABLE_SSL" };

        private sealed class ConfigEmMemoria : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> Valores = new();
            public Task<string> GetValorAsync(string chave) => Task.FromResult(Valores.TryGetValue(chave, out var v) ? v : null!);
            public Task SetValorAsync(string chave, string valor) { Valores[chave] = valor; return Task.CompletedTask; }
        }

        /// <summary>SMTP minimo: aceita uma mensagem e guarda o texto dela.</summary>
        private sealed class SmtpFalso : IDisposable
        {
            private readonly TcpListener _ouvinte = new(IPAddress.Loopback, 0);
            public int Porta => ((IPEndPoint)_ouvinte.LocalEndpoint).Port;
            public readonly List<string> Mensagens = new();

            public SmtpFalso()
            {
                _ouvinte.Start();
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        TcpClient cliente;
                        try { cliente = await _ouvinte.AcceptTcpClientAsync(); } catch { return; }
                        using (cliente)
                        {
                            var fluxo = cliente.GetStream();
                            var leitor = new StreamReader(fluxo, Encoding.ASCII);
                            var escritor = new StreamWriter(fluxo, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                            await escritor.WriteLineAsync("220 falso");
                            string? linha;
                            while ((linha = await leitor.ReadLineAsync()) != null)
                            {
                                var cmd = linha.ToUpperInvariant();
                                if (cmd.StartsWith("EHLO") || cmd.StartsWith("HELO")) await escritor.WriteLineAsync("250 ok");
                                else if (cmd.StartsWith("DATA"))
                                {
                                    await escritor.WriteLineAsync("354 manda");
                                    var corpo = new StringBuilder();
                                    while ((linha = await leitor.ReadLineAsync()) != null && linha != ".") corpo.AppendLine(linha);
                                    lock (Mensagens) Mensagens.Add(corpo.ToString());
                                    await escritor.WriteLineAsync("250 recebido");
                                }
                                else if (cmd.StartsWith("QUIT")) { await escritor.WriteLineAsync("221 tchau"); break; }
                                else await escritor.WriteLineAsync("250 ok");
                            }
                        }
                    }
                });
            }

            public void Dispose() => _ouvinte.Stop();
        }

        private readonly SmtpFalso _smtp = new();
        private readonly ConfigEmMemoria _config = new();

        public SmtpEmailServiceTests()
        {
            foreach (var v in Variaveis) Environment.SetEnvironmentVariable(v, null);
        }

        public void Dispose()
        {
            foreach (var v in Variaveis) Environment.SetEnvironmentVariable(v, null);
            _smtp.Dispose();
        }

        private sealed class ContaFixa : ConnectVeiculos.Core.Interfaces.Tenancy.ITenantContext
        {
            public bool IsResolved => true;
            public int TenantId => 7;
            public string TenantSlug => "auto-giro";
            public string DatabaseFile => "auto-giro.db";
            public void Resolve(int tenantId, string slug, string databaseFile) { }
        }

        private EmailPlataformaSettings Plataforma() => new()
        {
            SmtpServer = "127.0.0.1", SmtpPort = _smtp.Porta, EnableSsl = false,
            SenderEmail = "nao-responda@connectveiculos.dev.br", SenderName = "ConnectVeiculos",
            PublicSiteUrl = "https://connectveiculos.dev.br/"
        };

        /// <summary>Corpo da mensagem como texto (o .NET manda em base64 ou quoted-printable).</summary>
        private static string Corpo(string mensagem)
        {
            var linhas = mensagem.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            var resto = linhas.Skip(linhas.FindIndex(l => l.Length == 0) + 1).ToList();
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(resto))); }
            catch (FormatException)
            {
                // quoted-printable: "=" no fim da linha e' quebra suave.
                var qp = string.Concat(resto.Select(l => l.EndsWith('=') ? l[..^1] : l + "\n"));
                return qp.Replace("=3D", "=").Replace("=26", "&");
            }
        }

        private SmtpEmailService Servico(EmailSettings? appsettings = null, EmailPlataformaSettings? plataforma = null)
        {
            var loja = new Loja();
            typeof(Loja).GetProperty(nameof(Loja.LojId))!.SetValue(loja, 1);
            typeof(Loja).GetProperty(nameof(Loja.LojNome))!.SetValue(loja, "Diamante Veículos");
            typeof(Loja).GetProperty(nameof(Loja.LojEmail))!.SetValue(loja, "contato@diamanteveiculos.com.br");
            var lojas = new Mock<ILojaRepository>();
            lojas.Setup(l => l.GetAllAsync()).ReturnsAsync(new[] { loja });
            return new SmtpEmailService(Options.Create(appsettings ?? new EmailSettings()), _config, lojas.Object, NullLogger<SmtpEmailService>.Instance,
                plataforma == null ? null : Options.Create(plataforma), new ContaFixa());
        }

        private void SmtpDaLoja(string? nome = null)
        {
            _config.Valores["SMTP_SERVER"] = "127.0.0.1";
            _config.Valores["SMTP_PORT"] = _smtp.Porta.ToString();
            _config.Valores["SMTP_SENDER_EMAIL"] = "vendas@diamanteveiculos.com.br";
            _config.Valores["SMTP_ENABLE_SSL"] = "false";
            if (nome != null) _config.Valores["SMTP_SENDER_NAME"] = nome;
        }

        [Fact]
        public async Task Sai_pelo_email_da_loja_e_sem_nome_cadastrado_usa_o_nome_da_loja()
        {
            SmtpDaLoja();

            var ok = await Servico().SendEmailAsync("cliente@exemplo.com", "Preço caiu", "<p>oi</p>");

            ok.Should().BeTrue();
            var msg = _smtp.Mensagens.Single();
            msg.Should().Contain("<vendas@diamanteveiculos.com.br>");
            // Nome da loja, codificado por causa do acento ("Diamante Veículos").
            msg.Should().Contain("From: =?utf-8?Q?Diamante_Ve=C3=ADculos?=");
        }

        [Fact]
        public async Task Nome_cadastrado_pela_loja_vale_e_nada_da_plataforma_se_mistura()
        {
            // Variaveis antigas da plataforma no ambiente: tem que ser ignoradas.
            Environment.SetEnvironmentVariable("EMAIL_SMTP_SERVER", "smtp.plataforma.invalid");
            Environment.SetEnvironmentVariable("EMAIL_SENDER_EMAIL", "nao-responda@connectveiculos.dev.br");
            Environment.SetEnvironmentVariable("EMAIL_PASSWORD", "senha-da-plataforma");
            SmtpDaLoja("Diamante");

            var ok = await Servico().SendEmailAsync("cliente@exemplo.com", "Teste", "<p>oi</p>");

            ok.Should().BeTrue();
            var msg = _smtp.Mensagens.Single();
            msg.Should().Contain("From: \"Diamante\" <vendas@diamanteveiculos.com.br>");
            msg.Should().NotContain("connectveiculos");
        }

        [Fact]
        public async Task Sem_email_da_loja_nao_envia_mesmo_com_remetente_da_plataforma_configurado()
        {
            Environment.SetEnvironmentVariable("EMAIL_SMTP_SERVER", "127.0.0.1");
            Environment.SetEnvironmentVariable("EMAIL_SMTP_PORT", _smtp.Porta.ToString());
            Environment.SetEnvironmentVariable("EMAIL_SENDER_EMAIL", "nao-responda@connectveiculos.dev.br");
            Environment.SetEnvironmentVariable("EMAIL_ENABLE_SSL", "false");
            var appsettings = new EmailSettings
            {
                SmtpServer = "127.0.0.1", SmtpPort = _smtp.Porta, SenderEmail = "contato.connectveiculos@gmail.com", EnableSsl = false
            };

            var servico = Servico(appsettings);
            var ok = await servico.SendEmailAsync("cliente@exemplo.com", "Teste", "<p>oi</p>");
            var config = await servico.GetConfigAsync();

            ok.Should().BeFalse();
            _smtp.Mensagens.Should().BeEmpty();
            config.Configurado.Should().BeFalse();
        }

        [Fact]
        public async Task Redefinicao_de_senha_sai_pela_plataforma_mesmo_sem_email_da_loja()
        {
            var ok = await Servico(plataforma: Plataforma()).SendRecuperacaoSenhaAsync("dono@lojanova.com.br", "João", "abc123");

            ok.Should().BeTrue();
            var msg = _smtp.Mensagens.Single();
            msg.Should().Contain("From: \"ConnectVeiculos\" <nao-responda@connectveiculos.dev.br>");
            // Loja sem site proprio: o link usa o endereco do sistema e leva a conta.
            Corpo(msg).Should().Contain("https://connectveiculos.dev.br/redefinir-senha?token=abc123&tenant=auto-giro");
        }

        [Fact]
        public async Task Redefinicao_de_senha_sai_pela_plataforma_mesmo_com_email_da_loja()
        {
            SmtpDaLoja("Diamante");

            await Servico(plataforma: Plataforma()).SendRecuperacaoSenhaAsync("dono@lojanova.com.br", "João", "abc123");

            _smtp.Mensagens.Single().Should().Contain("<nao-responda@connectveiculos.dev.br>");
        }

        [Fact]
        public async Task Sem_remetente_da_plataforma_a_redefinicao_sai_pela_loja()
        {
            SmtpDaLoja("Diamante");

            var ok = await Servico().SendRecuperacaoSenhaAsync("dono@lojanova.com.br", "João", "abc123");

            ok.Should().BeTrue();
            _smtp.Mensagens.Single().Should().Contain("From: \"Diamante\" <vendas@diamanteveiculos.com.br>");
        }

        [Fact]
        public async Task Remetente_da_plataforma_nao_envia_os_avisos_da_loja()
        {
            var ok = await Servico(plataforma: Plataforma()).SendEmailAsync("cliente@exemplo.com", "Preço caiu", "<p>oi</p>");

            ok.Should().BeFalse();
            _smtp.Mensagens.Should().BeEmpty();
        }
    }

    [CollectionDefinition("VariaveisDeAmbienteEmail", DisableParallelization = true)]
    public class VariaveisDeAmbienteEmailCollection { }
}
