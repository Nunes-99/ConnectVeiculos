using ConnectVeiculos.Core.Entities.TestDrives;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface ITestDriveNotificacaoService
    {
        Task<TestDriveNotificacaoResult> NotificarConfirmacaoAsync(TestDrive td);
        Task<TestDriveNotificacaoResult> NotificarCancelamentoAsync(TestDrive td);
        Task<TestDriveNotificacaoResult> NotificarLembreteAsync(TestDrive td);
    }

    public class TestDriveNotificacaoResult
    {
        public bool Enviada { get; set; }
        /// <summary>
        /// Motivo de nao envio (quando Enviada=false). Valores possiveis:
        /// "ok" — enviada com sucesso
        /// "nao-configurado" — admin nao configurou WhatsApp no /integracoes
        /// "sem-telefone" — cliente nao informou whatsApp
        /// "falha-envio" — erro do Meta/WhatsApp (template nao aprovado, numero invalido, etc)
        /// </summary>
        public string Motivo { get; set; } = "ok";
        public string? MensagemErro { get; set; }
    }
}
