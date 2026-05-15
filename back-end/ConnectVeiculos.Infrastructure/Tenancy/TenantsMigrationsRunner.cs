using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.IoC;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <summary>
    /// No startup, garante que (a) o banco master existe e (b) cada tenant ativo
    /// tem seu banco criado com schema atual. Idempotente — usa EnsureCreated.
    ///
    /// Importante: roda OFFLINE (sem request HTTP), entao nao tem ITenantContext
    /// scoped. Para cada tenant, criamos um DbContextOptions manual e construimos
    /// o ConnectVeiculosDbContext apontando para o banco daquele tenant.
    /// </summary>
    public sealed class TenantsMigrationsRunner
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<TenantsMigrationsRunner> _logger;

        public TenantsMigrationsRunner(IServiceProvider services, ILogger<TenantsMigrationsRunner> logger)
        {
            _services = services;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            using var scope = _services.CreateScope();
            var sp = scope.ServiceProvider;

            // 1) Master: cria se nao existe e popula tenant default na primeira vez.
            var master = sp.GetRequiredService<MasterDbContext>();
            await master.Database.EnsureCreatedAsync(ct);
            ApplyMasterSchemaUpdates(master);

            // 2) Auto-seed do tenant "default" se o master estiver vazio.
            //    Aponta para o arquivo configurado em DEFAULT_TENANT_DATABASE_FILE
            //    (default: "cliente.db", preservando o banco existente em prod).
            //    Isso elimina passo manual de migracao no primeiro deploy do
            //    multi-tenant — o sistema se auto-configura.
            if (!await master.Tenants.AnyAsync(ct))
            {
                var defaultDbFile = Environment.GetEnvironmentVariable("DEFAULT_TENANT_DATABASE_FILE") ?? "cliente.db";
                var defaultTenant = new Tenant("default", "Tenant Padrão", defaultDbFile);
                master.Tenants.Add(defaultTenant);
                await master.SaveChangesAsync(ct);
                _logger.LogInformation("Master vazio: tenant 'default' criado automaticamente, banco {File}", defaultDbFile);
            }

            // 3) Itera tenants ativos do master e roda EnsureCreated em cada banco.
            var store = sp.GetRequiredService<ITenantStore>();
            store.InvalidateCache(); // forca releitura caso tenha sido populado por outra instancia
            var tenants = await store.ListActiveAsync(ct);

            if (tenants.Count == 0)
            {
                _logger.LogWarning("Nenhum tenant ativo apos seed — algo errado");
                return;
            }

            var tenantFactory = sp.GetRequiredService<ITenantConnectionFactory>();
            var softDeleteInterceptor = sp.GetRequiredService<Database.Interceptors.SoftDeleteInterceptor>();

            foreach (var tenant in tenants)
            {
                var connStr = tenantFactory.GetConnectionStringForTenant(tenant.TenSlug, tenant.TenDatabaseFile);
                var optionsBuilder = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                    .UseSqlite(connStr)
                    .AddInterceptors(softDeleteInterceptor);

                using var ctx = new ConnectVeiculosDbContext(optionsBuilder.Options);
                await ctx.Database.EnsureCreatedAsync(ct);

                // Aplica schema updates legacy (colunas adicionadas via ALTER TABLE
                // em features anteriores). Idempotente — bancos novos ja tem todas
                // as colunas via EnsureCreated, bancos antigos recebem o que falta.
                DependencyInjectionExtensions.ApplySchemaUpdates(ctx);

                // Reconcilia UserEmailMap no master a partir dos usuarios deste tenant.
                // Idempotente: INSERT OR IGNORE pula e-mails ja registrados.
                ReconcileUserEmailMap(master, ctx, tenant);

                _logger.LogInformation("Tenant '{Slug}' ({Nome}): banco {File} pronto (schema atualizado)",
                    tenant.TenSlug, tenant.TenNome, tenant.TenDatabaseFile);
            }
        }

        /// <summary>
        /// Schema updates idempotentes no banco master (rodam todo startup).
        /// - Cria tabela UserEmailMap se nao existir.
        /// - Adiciona colunas TenGoogleVerifCode / TenFacebookVerifCode na tabela
        ///   Tenants se ainda nao existirem (necessarias para verificacao de
        ///   dominio multi-tenant em Google Merchant / Facebook Catalog).
        /// </summary>
        private static void ApplyMasterSchemaUpdates(MasterDbContext master)
        {
            var conn = master.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS UserEmailMap (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL UNIQUE,
                TenantId INTEGER NOT NULL,
                TenantSlug TEXT NOT NULL,
                CriadoEm TEXT NOT NULL
            )";
            cmd.ExecuteNonQuery();

            using var idxCmd = conn.CreateCommand();
            idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_UserEmailMap_TenantId ON UserEmailMap(TenantId)";
            idxCmd.ExecuteNonQuery();

            EnsureColumn(conn, "Tenants", "TenGoogleVerifCode", "TEXT NULL");
            EnsureColumn(conn, "Tenants", "TenFacebookVerifCode", "TEXT NULL");
        }

        /// <summary>
        /// ALTER TABLE ... ADD COLUMN idempotente para SQLite. Verifica via
        /// PRAGMA table_info se a coluna ja existe antes de tentar criar.
        /// </summary>
        private static void EnsureColumn(System.Data.Common.DbConnection conn, string table, string column, string typeDef)
        {
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = $"PRAGMA table_info({table})";
                using var reader = pragma.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase)) return;
                }
            }

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {typeDef}";
            alter.ExecuteNonQuery();
        }

        /// <summary>
        /// Insere e-mails dos usuarios do tenant no UserEmailMap do master.
        /// INSERT OR IGNORE — duplicados (mesmo email ja registrado por outro tenant)
        /// sao silenciosamente ignorados. Util para auto-popular o registry quando
        /// a feature foi adicionada em sistema com dados existentes.
        /// </summary>
        private void ReconcileUserEmailMap(MasterDbContext master, ConnectVeiculosDbContext tenantCtx, Tenant tenant)
        {
            try
            {
                var tenantConn = tenantCtx.Database.GetDbConnection();
                if (tenantConn.State != System.Data.ConnectionState.Open) tenantConn.Open();

                var emails = new List<string>();
                using (var cmd = tenantConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT UsuEmail FROM Usuario WHERE UsuSts=1";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var email = reader.GetString(0)?.Trim().ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(email)) emails.Add(email);
                    }
                }

                if (emails.Count == 0) return;

                var masterConn = master.Database.GetDbConnection();
                if (masterConn.State != System.Data.ConnectionState.Open) masterConn.Open();
                using var tx = masterConn.BeginTransaction();
                using (var insCmd = masterConn.CreateCommand())
                {
                    insCmd.Transaction = tx;
                    insCmd.CommandText = "INSERT OR IGNORE INTO UserEmailMap (Email, TenantId, TenantSlug, CriadoEm) VALUES ($email, $tenantId, $slug, $criadoEm)";
                    var pEmail = insCmd.CreateParameter(); pEmail.ParameterName = "$email"; insCmd.Parameters.Add(pEmail);
                    var pTid = insCmd.CreateParameter(); pTid.ParameterName = "$tenantId"; pTid.Value = tenant.TenId; insCmd.Parameters.Add(pTid);
                    var pSlug = insCmd.CreateParameter(); pSlug.ParameterName = "$slug"; pSlug.Value = tenant.TenSlug; insCmd.Parameters.Add(pSlug);
                    var pData = insCmd.CreateParameter(); pData.ParameterName = "$criadoEm"; pData.Value = DateTime.UtcNow.ToString("o"); insCmd.Parameters.Add(pData);

                    foreach (var email in emails)
                    {
                        pEmail.Value = email;
                        insCmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao reconciliar UserEmailMap para tenant '{Slug}'", tenant.TenSlug);
            }
        }
    }
}
