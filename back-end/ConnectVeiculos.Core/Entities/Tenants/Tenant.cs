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
    }
}
