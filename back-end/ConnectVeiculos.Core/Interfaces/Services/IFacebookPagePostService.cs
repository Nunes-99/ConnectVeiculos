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

        /// <summary>
        /// Publica veiculo na timeline da Page. Retorna resultado se publicou ou
        /// null se pulou. Hook deve persistir em VeiculoPublicacao.
        /// </summary>
        Task<PublicacaoResult?> PublicarVeiculoAsync(int veiculoId);

        /// <summary>Variante manual — ignora auto-post desabilitado.</summary>
        Task<PublicacaoResult?> PublicarManualAsync(int veiculoId);
    }

    public class FacebookPagePostConfigInfo
    {
        public bool PageConectada { get; set; }
        public string? PageId { get; set; }
        public string? PageNome { get; set; }
        public bool AutoPostHabilitado { get; set; }
    }
}
