namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// OAuth unificado da Meta (Facebook + Instagram). Um unico App registrado em
    /// developers.facebook.com (META_APP_ID / META_APP_SECRET nas env vars) serve todos
    /// os tenants — cada tenant autoriza a propria Page e conta IG Business.
    ///
    /// Fluxo:
    ///   1. Frontend chama BuildAuthorizeUrl, redireciona para o Facebook
    ///   2. Usuario aprova, Facebook retorna ao callback com ?code=...&state=...
    ///   3. Backend troca o code por user token long-lived (60 dias)
    ///   4. Frontend lista Pages disponiveis e usuario escolhe uma
    ///   5. Backend pega o Page Access Token (nao expira) e detecta IG Business linkado
    /// </summary>
    public interface IMetaOAuthService
    {
        /// <summary>Gera URL de autorizacao no Facebook. State e' guardado (DataProtection) para CSRF.</summary>
        Task<string> BuildAuthorizeUrlAsync(string redirectUri);

        /// <summary>Troca o code por user token long-lived e persiste para o tenant.</summary>
        Task<MetaOAuthCallbackResult> ExchangeCodeAsync(string code, string state, string redirectUri);

        /// <summary>Lista as Pages do usuario autenticado (a partir do user token salvo).</summary>
        Task<IReadOnlyList<MetaPageOption>> ListarPagesAsync();

        /// <summary>Persiste a Page escolhida (page_id + page_access_token + IG Business linkado).</summary>
        Task<MetaSelectPageResult> SelecionarPageAsync(string pageId);

        /// <summary>Estado atual da conexao Meta para este tenant.</summary>
        Task<MetaConnectionInfo> GetConnectionInfoAsync();

        /// <summary>Limpa todos os tokens Meta (user/page/IG).</summary>
        Task DesconectarAsync();
    }

    public class MetaOAuthCallbackResult
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
        public string? UserNome { get; set; }
        public int PagesEncontradas { get; set; }
    }

    public class MetaPageOption
    {
        public string PageId { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Categoria { get; set; } = "";
        public bool TemInstagramBusiness { get; set; }
        public string? InstagramUsername { get; set; }
    }

    public class MetaSelectPageResult
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
        public string PageId { get; set; } = "";
        public string PageNome { get; set; } = "";
        public bool InstagramConectado { get; set; }
        public string? InstagramUsername { get; set; }
    }

    public class MetaConnectionInfo
    {
        public bool UserTokenDefinido { get; set; }
        public bool PageSelecionada { get; set; }
        public string? PageId { get; set; }
        public string? PageNome { get; set; }
        public bool InstagramConectado { get; set; }
        public string? InstagramBusinessId { get; set; }
        public string? InstagramUsername { get; set; }
        public DateTime? UserTokenExpiraEm { get; set; }
    }
}
