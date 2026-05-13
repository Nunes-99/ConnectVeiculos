using ConnectVeiculos.Core.Entities.TestDrives;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Notificacao
{
    /// <summary>
    /// Orquestra o envio de notificacoes WhatsApp para eventos de TestDrive
    /// (confirmacao, cancelamento, lembrete). Templates devem estar pre-aprovados
    /// no Meta Business pelo admin do tenant.
    ///
    /// Quando WhatsApp nao esta configurado ou template falha, retorna result com
    /// motivo apropriado — controller usa pra mostrar mensagem certa no frontend.
    /// </summary>
    public sealed class TestDriveNotificacaoService : ITestDriveNotificacaoService
    {
        private readonly IWhatsAppService _whatsApp;
        private readonly ConnectVeiculosDbContext _db;
        private readonly ILogger<TestDriveNotificacaoService> _logger;

        // Nomes dos templates que o admin deve criar no Meta Business
        public const string TEMPLATE_CONFIRMADO = "testdrive_confirmado";
        public const string TEMPLATE_CANCELADO = "testdrive_cancelado";
        public const string TEMPLATE_LEMBRETE = "testdrive_lembrete";
        public const string TEMPLATE_LANG = "pt_BR";

        public TestDriveNotificacaoService(
            IWhatsAppService whatsApp,
            ConnectVeiculosDbContext db,
            ILogger<TestDriveNotificacaoService> logger)
        {
            _whatsApp = whatsApp;
            _db = db;
            _logger = logger;
        }

        public Task<TestDriveNotificacaoResult> NotificarConfirmacaoAsync(TestDrive td)
            => EnviarAsync(td, TEMPLATE_CONFIRMADO, incluirEndereco: true);

        public Task<TestDriveNotificacaoResult> NotificarCancelamentoAsync(TestDrive td)
            => EnviarAsync(td, TEMPLATE_CANCELADO, incluirEndereco: false);

        public Task<TestDriveNotificacaoResult> NotificarLembreteAsync(TestDrive td)
            => EnviarAsync(td, TEMPLATE_LEMBRETE, incluirEndereco: true);

        private async Task<TestDriveNotificacaoResult> EnviarAsync(TestDrive td, string template, bool incluirEndereco)
        {
            var result = new TestDriveNotificacaoResult();

            if (!await _whatsApp.IsConfiguredAsync())
            {
                result.Enviada = false;
                result.Motivo = "nao-configurado";
                return result;
            }

            var telefone = NormalizarTelefoneE164(td.TdrWhatsApp ?? td.TdrTelefone);
            if (string.IsNullOrEmpty(telefone))
            {
                result.Enviada = false;
                result.Motivo = "sem-telefone";
                return result;
            }

            // Busca dados do veiculo e da loja pra preencher os parametros do template
            var veiculo = await _db.Veiculos.AsNoTracking().FirstOrDefaultAsync(v => v.VeiId == td.R_VeiId);
            var veiculoDesc = veiculo == null
                ? "Veiculo"
                : $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}".Trim();

            string lojaNome = "Concessionaria";
            string lojaEndereco = "";
            if (td.R_LojId.HasValue)
            {
                var loja = await _db.Lojas.AsNoTracking().FirstOrDefaultAsync(l => l.LojId == td.R_LojId.Value);
                if (loja != null)
                {
                    lojaNome = loja.LojNome ?? lojaNome;
                    var partes = new[] { loja.LojLogradouro, loja.LojNumero, loja.LojBairro, loja.LojCidade, loja.LojEstado }
                        .Where(p => !string.IsNullOrWhiteSpace(p));
                    lojaEndereco = string.Join(", ", partes);
                }
            }

            var parametros = new List<string>
            {
                td.TdrNomeCliente ?? "Cliente",
                td.TdrDataAgendamento.ToString("dd/MM/yyyy"),
                td.TdrHorario ?? "",
                veiculoDesc
            };

            if (incluirEndereco)
            {
                parametros.Add(string.IsNullOrEmpty(lojaEndereco) ? lojaNome : lojaEndereco);
                parametros.Add(lojaNome);
            }
            else
            {
                // Templates sem endereco (cancelamento) tem so {{5}} = nome loja
                parametros.Add(lojaNome);
            }

            try
            {
                var ok = await _whatsApp.EnviarTemplateAsync(telefone, template, TEMPLATE_LANG, parametros);
                if (ok)
                {
                    result.Enviada = true;
                    result.Motivo = "ok";
                    _logger.LogInformation("TestDrive notificacao enviada: template={Template} telefone={Telefone} testDriveId={Id}",
                        template, telefone, td.TdrId);
                }
                else
                {
                    result.Enviada = false;
                    result.Motivo = "falha-envio";
                    result.MensagemErro = "Meta WhatsApp retornou erro. Confira se o template '" + template + "' esta aprovado.";
                    _logger.LogWarning("TestDrive notificacao falhou: template={Template} testDriveId={Id}", template, td.TdrId);
                }
            }
            catch (Exception ex)
            {
                result.Enviada = false;
                result.Motivo = "falha-envio";
                result.MensagemErro = ex.Message;
                _logger.LogError(ex, "Erro enviando notificacao WhatsApp testDriveId={Id}", td.TdrId);
            }

            return result;
        }

        /// <summary>
        /// Normaliza telefone para formato E.164 brasileiro (+55DDXXXXXXXXX).
        /// Aceita varios formatos comuns: "(11) 99999-9999", "11999999999", "+5511999999999".
        /// </summary>
        private static string NormalizarTelefoneE164(string? telefone)
        {
            if (string.IsNullOrWhiteSpace(telefone)) return "";
            var digitos = new string(telefone.Where(char.IsDigit).ToArray());
            if (digitos.Length == 0) return "";
            // Se ja vem com 55 prefixado e tem 12+ digitos
            if (digitos.Length >= 12 && digitos.StartsWith("55")) return digitos;
            // 10 digitos (fixo) ou 11 digitos (celular com 9): adiciona 55
            if (digitos.Length == 10 || digitos.Length == 11) return "55" + digitos;
            return "";
        }
    }
}
