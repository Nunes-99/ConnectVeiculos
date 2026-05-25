namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IMercadoLivreService
    {
        string GetAuthUrl();
         // State e o parametro `state` recebido no callback OAuth — vem cifrado pelo
         // OAuthStateProtector. Sem ele o callback rejeita o code (proteção CSRF).
         Task HandleCallbackAsync(string code, string? state);
        Task<bool> IsConnectedAsync();
        Task<MercadoLivreContaInfo?> GetContaInfoAsync();
        Task DesconectarAsync();
        Task<(string ExternoId, string Url)> PublicarVeiculoAsync(int veiculoId);
        Task RemoverAnuncioAsync(string externoId);
        Task AtualizarAnuncioAsync(string externoId, int veiculoId);

         // Processa uma notificacao recebida do webhook do ML. Topic mais comum
         // e "items" (mudanca de status de anuncio). Outros topics sao logados
         // e ignorados ate termos handler dedicado.
         Task ProcessarNotificacaoAsync(string topic, string resource);
    }

    public class MercadoLivreContaInfo
    {
        public string Nickname { get; set; } = "";
        public string Email { get; set; } = "";
        public string UserId { get; set; } = "";
        public string? Pais { get; set; }
        public string? UrlPerfil { get; set; }
    }
}
