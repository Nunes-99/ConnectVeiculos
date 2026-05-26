namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Post organico na timeline de uma Facebook Page. Diferente do Catalog (que alimenta
    /// Vehicle Ads pagos), aqui o post aparece como conteudo grauito no feed dos seguidores.
    /// </summary>
    public interface IFacebookPagePostService
    {
        Task<bool> IsConfiguredAsync();
        Task<FacebookPagePostConfigInfo> GetConfigAsync();
        Task SetAutoPostHabilitadoAsync(bool habilitado);
        Task<TestIntegracaoResult> TestarAsync();
        Task PublicarVeiculoAsync(int veiculoId);
    }

    public class FacebookPagePostConfigInfo
    {
        public bool PageConectada { get; set; }
        public string? PageId { get; set; }
        public string? PageNome { get; set; }
        public bool AutoPostHabilitado { get; set; }
    }
}
