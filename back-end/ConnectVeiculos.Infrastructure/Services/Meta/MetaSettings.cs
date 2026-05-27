namespace ConnectVeiculos.Infrastructure.Services.Meta
{
    /// <summary>
    /// Configuracao GLOBAL do App da Meta. Um unico App serve todos os tenants —
    /// cada tenant autoriza a propria Page via OAuth.
    ///
    /// Cadastre em developers.facebook.com > seu app > Configuracoes Basicas e
    /// defina via env vars no docker-compose:
    ///   - MetaSettings__AppId
    ///   - MetaSettings__AppSecret
    ///   - MetaSettings__ApiVersion (opcional, default v18.0)
    ///   - MetaSettings__PublicSiteUrl (fallback igual ao Catalog/Google)
    ///   - MetaSettings__InstagramEnabled (master switch — exibe/oculta card no front)
    /// </summary>
    public class MetaSettings
    {
        public string AppId { get; set; } = "";
        public string AppSecret { get; set; } = "";
        public string ApiVersion { get; set; } = "v18.0";

        /// <summary>URL publica do site para montar links/imagens dos posts.</summary>
        public string PublicSiteUrl { get; set; } = "";

        /// <summary>
        /// Liga/desliga o card Meta (Facebook Page + Instagram) na tela de
        /// integracoes. False enquanto App Meta nao tiver passado em Business
        /// Verification + App Review (ver memoria project_pendente_meta_app_review).
        /// Pode ser usado em conjunto com modo Tester (ate 100 contas adicionadas
        /// manualmente no painel Meta conseguem usar mesmo em Development Mode).
        /// </summary>
        public bool InstagramEnabled { get; set; } = false;
    }
}
