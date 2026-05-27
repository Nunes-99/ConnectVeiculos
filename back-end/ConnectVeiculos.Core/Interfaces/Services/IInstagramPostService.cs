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

        /// <summary>
        /// Publica veiculo no feed do IG. Retorna o resultado se publicou, ou
        /// null se pulou (auto-post desabilitado, sem credenciais, sem imagens,
        /// rate-limit, etc). Hook deve persistir o resultado em VeiculoPublicacao.
        /// </summary>
        Task<PublicacaoResult?> PublicarVeiculoAsync(int veiculoId);

        /// <summary>
        /// Variante manual: publica mesmo se auto-post estiver desabilitado.
        /// Respeita demais restricoes (credenciais, imagens, rate limit).
        /// Usado pelo endpoint "Publicar agora" na tela de veiculos.
        /// </summary>
        Task<PublicacaoResult?> PublicarManualAsync(int veiculoId);
    }

    public class InstagramPostConfigInfo
    {
        public bool InstagramConectado { get; set; }
        public string? BusinessAccountId { get; set; }
        public string? Username { get; set; }
        public bool AutoPostHabilitado { get; set; }
    }

    /// <summary>
    /// Resultado de uma publicacao em rede social. Usado pra persistir em
    /// VeiculoPublicacao (historico + impedir republicacao).
    /// </summary>
    public class PublicacaoResult
    {
        public string ExternoId { get; set; } = "";
        public string Url { get; set; } = "";
    }
}
