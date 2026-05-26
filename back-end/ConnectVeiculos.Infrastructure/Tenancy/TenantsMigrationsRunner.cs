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

             // Billing / planos (Camada 1: limites por plano + trial 30d).
             // Tabela Planos + colunas TenPlaId/TenTrialAte sao idempotentes.
             using (var planoCmd = conn.CreateCommand())
             {
                 planoCmd.CommandText = @"CREATE TABLE IF NOT EXISTS Planos (
                     PlaId INTEGER PRIMARY KEY AUTOINCREMENT,
                     PlaNome TEXT NOT NULL UNIQUE,
                     PlaPreco TEXT NOT NULL DEFAULT '0',
                     PlaMaxVeiculos INTEGER NULL,
                     PlaMaxLojas INTEGER NULL,
                     PlaMaxUsuarios INTEGER NULL,
                     PlaMaxLeadsMes INTEGER NULL,
                     PlaOrdem INTEGER NOT NULL DEFAULT 0,
                     PlaAtivo INTEGER NOT NULL DEFAULT 1,
                     PlaDtCriacao TEXT NOT NULL
                 )";
                 planoCmd.ExecuteNonQuery();
             }

             EnsureColumn(conn, "Tenants", "TenPlaId", "INTEGER NULL");
             EnsureColumn(conn, "Tenants", "TenTrialAte", "TEXT NULL");

             // Seed dos planos default. UPSERT manual (insere se o nome ainda nao
             // existe). Permite atualizar limites ajustando a config aqui — basta
             // mudar nome ou apagar e recriar; tenants existentes mantem o
             // TenPlaId que ja tinham.
             SeedPlanoSeNaoExistir(conn, "Free",       0,    5,   1,  1,   20, 1);
             SeedPlanoSeNaoExistir(conn, "Basic",     99,   50,   1,  3,  200, 2);
             SeedPlanoSeNaoExistir(conn, "Pro",      299,  500,   3, 10, 2000, 3);
             SeedPlanoSeNaoExistir(conn, "Enterprise", 0, null, null, null, null, 4);

             // Tenants sem plano atribuido -> Free, com trial 30 dias a partir do
             // momento que essa migration roda (idempotente: so atribui se TenPlaId IS NULL).
             AtribuirPlanoDefaultParaTenantsExistentes(conn);
        }

         private static void SeedPlanoSeNaoExistir(System.Data.Common.DbConnection conn, string nome,
             decimal preco, int? maxVei, int? maxLojas, int? maxUsu, int? maxLeads, int ordem)
         {
             using var check = conn.CreateCommand();
             check.CommandText = "SELECT COUNT(*) FROM Planos WHERE PlaNome=$nome";
             var pN = check.CreateParameter(); pN.ParameterName = "$nome"; pN.Value = nome; check.Parameters.Add(pN);
             if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;

             using var ins = conn.CreateCommand();
             ins.CommandText = @"INSERT INTO Planos
                 (PlaNome, PlaPreco, PlaMaxVeiculos, PlaMaxLojas, PlaMaxUsuarios, PlaMaxLeadsMes, PlaOrdem, PlaAtivo, PlaDtCriacao)
                 VALUES ($nome, $preco, $mv, $ml, $mu, $mle, $ordem, 1, $dt)";
             AddParam(ins, "$nome", nome);
             AddParam(ins, "$preco", preco.ToString(System.Globalization.CultureInfo.InvariantCulture));
             AddParam(ins, "$mv", (object?)maxVei ?? DBNull.Value);
             AddParam(ins, "$ml", (object?)maxLojas ?? DBNull.Value);
             AddParam(ins, "$mu", (object?)maxUsu ?? DBNull.Value);
             AddParam(ins, "$mle", (object?)maxLeads ?? DBNull.Value);
             AddParam(ins, "$ordem", ordem);
             AddParam(ins, "$dt", DateTime.UtcNow.ToString("o"));
             ins.ExecuteNonQuery();
         }

         private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
         {
             var p = cmd.CreateParameter();
             p.ParameterName = name;
             p.Value = value;
             cmd.Parameters.Add(p);
         }

         private static void AtribuirPlanoDefaultParaTenantsExistentes(System.Data.Common.DbConnection conn)
         {
             // Pega o id do plano Free.
             using var sel = conn.CreateCommand();
             sel.CommandText = "SELECT PlaId FROM Planos WHERE PlaNome='Free' LIMIT 1";
             var freeId = sel.ExecuteScalar();
             if (freeId == null || freeId == DBNull.Value) return;

             // Atribui plano Free + trial 30d para qualquer tenant ainda sem plano.
             using var upd = conn.CreateCommand();
             upd.CommandText = @"UPDATE Tenants
                 SET TenPlaId=$pid, TenTrialAte=$ate
                 WHERE TenPlaId IS NULL";
             AddParam(upd, "$pid", Convert.ToInt32(freeId));
             AddParam(upd, "$ate", DateTime.UtcNow.AddDays(30).ToString("o"));
             upd.ExecuteNonQuery();
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
