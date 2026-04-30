namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IMercadoLivreService
    {
        string GetAuthUrl();
        Task HandleCallbackAsync(string code);
        Task<bool> IsConnectedAsync();
        Task<MercadoLivreContaInfo?> GetContaInfoAsync();
        Task DesconectarAsync();
        Task<(string ExternoId, string Url)> PublicarVeiculoAsync(int veiculoId);
        Task RemoverAnuncioAsync(string externoId);
        Task AtualizarAnuncioAsync(string externoId, int veiculoId);
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
