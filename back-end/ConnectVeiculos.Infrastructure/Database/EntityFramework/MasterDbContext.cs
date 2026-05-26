using ConnectVeiculos.Core.Entities.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework
{
    /// <summary>
    /// DbContext apenas para o registry de tenants (data/_master.db). Separado
    /// do ConnectVeiculosDbContext para nao se confundir com os bancos de tenant.
    /// </summary>
    public sealed class MasterDbContext : DbContext
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<UserEmailMap> UserEmailMaps => Set<UserEmailMap>();
         public DbSet<Plano> Planos => Set<Plano>();

        public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var t = modelBuilder.Entity<Tenant>();
            t.ToTable("Tenants");
            t.HasKey(x => x.TenId);
            t.Property(x => x.TenId).ValueGeneratedOnAdd();
            t.Property(x => x.TenSlug).IsRequired().HasMaxLength(64);
            t.HasIndex(x => x.TenSlug).IsUnique();
            t.Property(x => x.TenNome).IsRequired().HasMaxLength(200);
            t.Property(x => x.TenDatabaseFile).IsRequired().HasMaxLength(255);
            t.Property(x => x.TenStatus).HasConversion<int>();
            t.Property(x => x.TenDtCriacao);
            t.Property(x => x.TenGoogleVerifCode).HasMaxLength(128);
            t.Property(x => x.TenFacebookVerifCode).HasMaxLength(128);
             t.Property(x => x.TenPlaId);
             t.Property(x => x.TenTrialAte);

             var p = modelBuilder.Entity<Plano>();
             p.ToTable("Planos");
             p.HasKey(x => x.PlaId);
             p.Property(x => x.PlaId).ValueGeneratedOnAdd();
             p.Property(x => x.PlaNome).IsRequired().HasMaxLength(64);
             p.HasIndex(x => x.PlaNome).IsUnique();
             p.Property(x => x.PlaPreco).HasPrecision(10, 2);
             p.Property(x => x.PlaMaxVeiculos);
             p.Property(x => x.PlaMaxLojas);
             p.Property(x => x.PlaMaxUsuarios);
             p.Property(x => x.PlaMaxLeadsMes);
             p.Property(x => x.PlaOrdem);
             p.Property(x => x.PlaAtivo);
             p.Property(x => x.PlaDtCriacao);

            var u = modelBuilder.Entity<UserEmailMap>();
            u.ToTable("UserEmailMap");
            u.HasKey(x => x.Id);
            u.Property(x => x.Id).ValueGeneratedOnAdd();
            u.Property(x => x.Email).IsRequired().HasMaxLength(255);
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.TenantSlug).IsRequired().HasMaxLength(64);
            u.HasIndex(x => x.TenantId);
        }
    }
}
