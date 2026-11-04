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
    /// Quem envia o e-mail: o SMTP da loja (se ela cadastrou) ou o da
    /// plataforma (.env), sempre como um bloco so. Um servidor SMTP falso
    /// local guarda o que chegou para conferir remetente e Reply-To.
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

        private SmtpEmailService Servico()
        {
            var loja = new Loja();
            typeof(Loja).GetProperty(nameof(Loja.LojId))!.SetValue(loja, 1);
            typeof(Loja).GetProperty(nameof(Loja.LojNome))!.SetValue(loja, "Diamante Veículos");
            typeof(Loja).GetProperty(nameof(Loja.LojEmail))!.SetValue(loja, "contato@diamanteveiculos.com.br");
            var lojas = new Mock<ILojaRepository>();
            lojas.Setup(l => l.GetAllAsync()).ReturnsAsync(new[] { loja });
            return new SmtpEmailService(Options.Create(new EmailSettings()), _config, lojas.Object, NullLogger<SmtpEmailService>.Instance);
        }

        private void PlataformaNoEnv(int porta)
        {
            Environment.SetEnvironmentVariable("EMAIL_SMTP_SERVER", "127.0.0.1");
            Environment.SetEnvironmentVariable("EMAIL_SMTP_PORT", porta.ToString());
            Environment.SetEnvironmentVariable("EMAIL_SENDER_EMAIL", "nao-responda@connectveiculos.dev.br");
            Environment.SetEnvironmentVariable("EMAIL_ENABLE_SSL", "false");
        }

        [Fact]
        public async Task Sem_SMTP_da_loja_sai_pela_plataforma_com_o_nome_da_loja_e_resposta_para_ela()
        {
            PlataformaNoEnv(_smtp.Porta);

            var ok = await Servico().SendEmailAsync("cliente@exemplo.com", "Preço caiu", "<p>oi</p>");

            ok.Should().BeTrue();
            var msg = _smtp.Mensagens.Single();
            msg.Should().Contain("nao-responda@connectveiculos.dev.br");
            // Cabecalho pode quebrar linha: (?s) deixa o .* atravessar.
            msg.Should().MatchRegex("(?is)Reply-To:[^:]*contato@diamanteveiculos\\.com\\.br");
            // Nome da loja, codificado por causa do acento ("Diamante Veículos").
            msg.Should().Contain("From: =?utf-8?Q?Diamante_Ve=C3=ADculos?=");
        }

        [Fact]
        public async Task SMTP_da_loja_ganha_do_env_e_nao_mistura_campos()
        {
            // Plataforma apontando para uma porta morta: se algum campo dela
            // vazasse para o envio da loja, o envio falharia.
            PlataformaNoEnv(1);
            Environment.SetEnvironmentVariable("EMAIL_PASSWORD", "senha-da-plataforma");
            _config.Valores["SMTP_SERVER"] = "127.0.0.1";
            _config.Valores["SMTP_PORT"] = _smtp.Porta.ToString();
            _config.Valores["SMTP_SENDER_EMAIL"] = "vendas@diamanteveiculos.com.br";
            _config.Valores["SMTP_SENDER_NAME"] = "Diamante";
            _config.Valores["SMTP_ENABLE_SSL"] = "false";

            var ok = await Servico().SendEmailAsync("cliente@exemplo.com", "Teste", "<p>oi</p>");

            ok.Should().BeTrue();
            var msg = _smtp.Mensagens.Single();
            msg.Should().Contain("vendas@diamanteveiculos.com.br");
            msg.Should().NotContain("nao-responda@connectveiculos.dev.br");
            msg.Should().NotMatchRegex("(?i)Reply-To:", "o e-mail ja sai da propria loja");
        }

        [Fact]
        public async Task Sem_nada_configurado_nao_envia()
        {
            var ok = await Servico().SendEmailAsync("cliente@exemplo.com", "Teste", "<p>oi</p>");

            ok.Should().BeFalse();
            _smtp.Mensagens.Should().BeEmpty();
        }
    }

    [CollectionDefinition("VariaveisDeAmbienteEmail", DisableParallelization = true)]
    public class VariaveisDeAmbienteEmailCollection { }
}
