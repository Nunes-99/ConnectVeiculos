namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Post no feed do Instagram Business linkado a Facebook Page. Cria carrossel
    /// (ate 10 fotos) ou foto unica via Instagram Graph API.
    /// </summary>
    public interface IInstagramPostService
    {
        Task<bool> IsConfiguredAsync();
        Task<InstagramPostConfigInfo> GetConfigAsync();
        Task SetAutoPostHabilitadoAsync(bool habilitado);
        Task<TestIntegracaoResult> TestarAsync();
        Task PublicarVeiculoAsync(int veiculoId);
    }

    public class InstagramPostConfigInfo
    {
        public bool InstagramConectado { get; set; }
        public string? BusinessAccountId { get; set; }
        public string? Username { get; set; }
        public bool AutoPostHabilitado { get; set; }
    }
}
