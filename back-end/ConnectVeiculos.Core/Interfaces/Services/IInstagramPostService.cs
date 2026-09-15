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

        /// <summary>
        /// Liga ou desliga a exclusao do post quando o veiculo sai de circulacao.
        ///
        /// Desligado por padrao, e de proposito: a Graph API nao deixa editar
        /// legenda de midia publicada, entao a unica forma de tirar do ar um
        /// anuncio de carro vendido e apagar o post — junto com as curtidas, os
        /// comentarios e o alcance dele, sem volta. Quem liga precisa saber disso.
        /// </summary>
        Task SetExcluirAoSairHabilitadoAsync(bool habilitado);

        /// <summary>
        /// Apaga um post do Instagram. Devolve false quando nao ha conexao, o
        /// post ja nao existe ou a Meta recusa — nunca lanca.
        /// </summary>
        Task<bool> ExcluirPostAsync(string mediaId);

        /// <summary>Se a exclusao automatica esta ligada neste tenant.</summary>
        Task<bool> ExcluirAoSairHabilitadoAsync();
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
        public bool ExcluirAoSairHabilitado { get; set; }
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
