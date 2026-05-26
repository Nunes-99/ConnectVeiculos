namespace ConnectVeiculos.Core.Entities.Tenants
{
    /// <summary>
    /// Registry de um tenant (cliente operador) no banco master. Cada tenant tem
    /// seu proprio banco de dados isolado em data/{slug}.db.
    /// </summary>
    public class Tenant
    {
        public int TenId { get; private set; }

        /// <summary>Identificador unico para subdomain (ex: "acme" -> acme.connectveiculos.dev.br).</summary>
        public string TenSlug { get; private set; } = string.Empty;

        /// <summary>Nome amigavel (ex: "Acme Veiculos LTDA").</summary>
        public string TenNome { get; private set; } = string.Empty;

        /// <summary>Nome do arquivo .db relativo ao diretorio data/. Default: "{slug}.db".</summary>
        public string TenDatabaseFile { get; private set; } = string.Empty;

        public TenantStatus TenStatus { get; private set; } = TenantStatus.Active;

        public DateTime TenDtCriacao { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// Codigo de verificacao do Google Search Console para este tenant.
        /// Apenas o "content" da meta tag (ex: "ABC123XYZ"), sem aspas nem tag.
        /// O SSR injeta no &lt;head&gt; uma &lt;meta name="google-site-verification"&gt;
        /// para CADA tenant que tenha codigo cadastrado, permitindo que o Google
        /// valide multiplas contas Merchant Center sobre o mesmo dominio raiz.
        /// </summary>
        public string? TenGoogleVerifCode { get; private set; }

        /// <summary>
        /// Codigo de verificacao de dominio do Meta/Facebook para este tenant.
        /// Apenas o "content" da meta tag, mesma logica do TenGoogleVerifCode.
        /// </summary>
        public string? TenFacebookVerifCode { get; private set; }

         // FK para Plano (no master). Default na migration: plano Free.
         public int? TenPlaId { get; private set; }

         // Trial expira em (UTC). Durante o trial, limites do plano sao IGNORADOS — tenant
         // tem acesso ilimitado. Apos a data, limites do TenPlaId voltam a valer. Null =
         // sem trial (tenant cadastrado antes da feature ou nunca esteve em trial).
         public DateTime? TenTrialAte { get; private set; }

        public Tenant() { }

        public Tenant(string slug, string nome, string? databaseFile = null)
        {
            TenSlug = slug;
            TenNome = nome;
            TenDatabaseFile = string.IsNullOrWhiteSpace(databaseFile) ? $"{slug}.db" : databaseFile;
            TenStatus = TenantStatus.Active;
            TenDtCriacao = DateTime.UtcNow;
        }

        public void Suspender() => TenStatus = TenantStatus.Suspended;
        public void Reativar() => TenStatus = TenantStatus.Active;

        public void Renomear(string novoNome) => TenNome = novoNome;

        public void SetGoogleVerifCode(string? code)
            => TenGoogleVerifCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

        public void SetFacebookVerifCode(string? code)
            => TenFacebookVerifCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

         public void AlterarPlano(int novoPlanoId) => TenPlaId = novoPlanoId;

         public void IniciarTrial(int dias) => TenTrialAte = DateTime.UtcNow.AddDays(dias);

         public bool EmTrial() => TenTrialAte.HasValue && TenTrialAte.Value > DateTime.UtcNow;
    }
}
