namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IWhatsAppService
    {
        Task<bool> IsConfiguredAsync();
        /// <summary>Devolve config atual (sem expor o access_token)</summary>
        Task<WhatsAppConfigInfo> GetConfigAsync();
        /// <summary>Salva credenciais no ConfiguracaoSistema (substitui qualquer valor anterior)</summary>
        Task SalvarConfigAsync(string accessToken, string phoneId, string verifyToken);
        /// <summary>Limpa as 3 chaves do ConfiguracaoSistema</summary>
        Task DesconectarAsync();
        /// <summary>Verify token configurado (usado pelo handler do webhook GET)</summary>
        Task<string?> GetVerifyTokenAsync();
        /// <summary>Envia uma mensagem de texto livre (somente em janela de 24h apos contato do cliente)</summary>
        Task<bool> EnviarMensagemAsync(string telefoneE164, string mensagem);
        /// <summary>Envia template aprovado pelo Meta (necessario para iniciar conversa)</summary>
        Task<bool> EnviarTemplateAsync(string telefoneE164, string templateName, string lang, IEnumerable<string> parametros);
    }

    public class WhatsAppConfigInfo
    {
        public bool Configurado { get; set; }
        public string? PhoneId { get; set; }
        public bool VerifyTokenDefinido { get; set; }
    }
}
