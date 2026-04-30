using ConnectVeiculos.Core.Entities.Acessos;
using ConnectVeiculos.Core.Entities.Caracteristicas;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.HistoricoPrecos;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.LojasUsuarios;
using ConnectVeiculos.Core.Entities.Logs;
using ConnectVeiculos.Core.Entities.Notificacoes;
using ConnectVeiculos.Core.Entities.Observacoes;
using ConnectVeiculos.Core.Entities.Permissoes;
using ConnectVeiculos.Core.Entities.RecuperacaoSenha;
using ConnectVeiculos.Core.Entities.RefreshTokens;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosCaracteristicas;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Entities.VeiculosObservacoes;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Entities.TestDrives;
using ConnectVeiculos.Core.Entities.Despesas;
using ConnectVeiculos.Core.Entities.Leads;
using ConnectVeiculos.Core.Entities.Favoritos;
using ConnectVeiculos.Core.Entities.Webhooks;
using ConnectVeiculos.Core.Entities.Configuracoes;
using ConnectVeiculos.Core.Entities.Documentos;
using ConnectVeiculos.Core.Entities.Negociacoes;
using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Entities.PushSubscriptions;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework
{
    public class ConnectVeiculosDbContext : DbContext
    {
        public ConnectVeiculosDbContext(DbContextOptions<ConnectVeiculosDbContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Loja> Lojas { get; set; }
        public DbSet<LojaUsuario> LojasUsuarios { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Veiculo> Veiculos { get; set; }
        public DbSet<Acesso> Acessos { get; set; }
        public DbSet<Permissao> Permissoes { get; set; }
        public DbSet<Caracteristica> Caracteristicas { get; set; }
        public DbSet<VeiculoCaracteristica> VeiculosCaracteristicas { get; set; }
        public DbSet<Observacao> Observacoes { get; set; }
        public DbSet<VeiculoObservacao> VeiculosObservacoes { get; set; }
        public DbSet<VeiculoImagem> VeiculosImagens { get; set; }
        public DbSet<Venda> Vendas { get; set; }
        public DbSet<LogAuditoria> LogsAuditoria { get; set; }
        public DbSet<HistoricoPreco> HistoricosPreco { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Notificacao> Notificacoes { get; set; }
        public DbSet<Webhook> Webhooks { get; set; }
        public DbSet<RecuperacaoSenha> RecuperacoesSenha { get; set; }
        public DbSet<TestDrive> TestDrives { get; set; }
        public DbSet<VeiculoDespesa> VeiculosDespesas { get; set; }
        public DbSet<Lead> Leads { get; set; }
        public DbSet<Favorito> Favoritos { get; set; }
        public DbSet<Negociacao> Negociacoes { get; set; }
        public DbSet<VeiculoPublicacao> VeiculoPublicacoes { get; set; }
        public DbSet<ConfiguracaoSistema> Configuracoes { get; set; }
        public DbSet<VeiculoDocumento> VeiculosDocumentos { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Usuario
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.ToTable("Usuario");
                entity.HasKey(e => e.UsuId);
                entity.Property(e => e.UsuId).ValueGeneratedOnAdd();
                entity.Property(e => e.UsuNome).HasMaxLength(200).IsRequired();
                entity.Property(e => e.UsuCPF).HasMaxLength(14);
                entity.Property(e => e.UsuRG).HasMaxLength(20);
                entity.Property(e => e.UsuEmail).HasMaxLength(255).IsRequired();
                entity.Property(e => e.UsuSenha).HasMaxLength(255).IsRequired();
                entity.Property(e => e.UsuFuncao).HasMaxLength(100);
            });

            // Loja
            modelBuilder.Entity<Loja>(entity =>
            {
                entity.ToTable("Loja");
                entity.HasKey(e => e.LojId);
                entity.Property(e => e.LojId).ValueGeneratedOnAdd();
                entity.Property(e => e.LojNome).HasMaxLength(200).IsRequired();
                entity.Property(e => e.LojLogradouro).HasMaxLength(255);
                entity.Property(e => e.LojNumero).HasMaxLength(50);
                entity.Property(e => e.LojBairro).HasMaxLength(150);
                entity.Property(e => e.LojCidade).HasMaxLength(150);
                entity.Property(e => e.LojEstado).HasMaxLength(2);
                entity.Property(e => e.LojCEP).HasMaxLength(9);
                entity.Property(e => e.LojComplemento).HasMaxLength(255);
                entity.Property(e => e.LojEmail).HasMaxLength(255);
                entity.Property(e => e.LojTel1).HasMaxLength(20);
                entity.Property(e => e.LojTel2).HasMaxLength(20);
                entity.Property(e => e.LojWhatsApp).HasMaxLength(20);
                entity.Property(e => e.LojImg).HasMaxLength(500);
                entity.Property(e => e.LojCNPJ).HasMaxLength(18);
                entity.Property(e => e.LojIE).HasMaxLength(20);
                entity.Property(e => e.LojCorPrimaria).HasMaxLength(20);
                entity.Property(e => e.LojCorSecundaria).HasMaxLength(20);
                entity.Property(e => e.LojInstagram).HasMaxLength(255);
                entity.Property(e => e.LojFacebook).HasMaxLength(255);
                entity.Property(e => e.LojSlug).HasMaxLength(100);
                entity.HasIndex(e => e.LojSlug).IsUnique();
                entity.Property(e => e.LojUrlCatalogo).HasMaxLength(500);
            });

            // LojaUsuario
            modelBuilder.Entity<LojaUsuario>(entity =>
            {
                entity.ToTable("LojaUsuario");
                entity.HasKey(e => e.LojUsuId);
                entity.Property(e => e.LojUsuId).ValueGeneratedOnAdd();
                entity.Property(e => e.UsuAcs).HasMaxLength(1);
                entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.R_UsuId);
                entity.HasOne(e => e.Loja).WithMany().HasForeignKey(e => e.R_LojId);
            });

            // Categoria
            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.ToTable("Categoria");
                entity.HasKey(e => e.CatId);
                entity.Property(e => e.CatId).ValueGeneratedOnAdd();
                entity.Property(e => e.CatNome).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CatDesc).HasMaxLength(255);
            });

            // Veiculo
            modelBuilder.Entity<Veiculo>(entity =>
            {
                entity.ToTable("Veiculo");
                entity.HasKey(e => e.VeiId);
                entity.Property(e => e.VeiId).ValueGeneratedOnAdd();
                entity.Property(e => e.VeiMarca).HasMaxLength(100);
                entity.Property(e => e.VeiModelo).HasMaxLength(150);
                entity.Property(e => e.VeiPlaca).HasMaxLength(10);
                entity.Property(e => e.VeiChassi).HasMaxLength(20);
                entity.Property(e => e.VeiCor).HasMaxLength(50);
                entity.Property(e => e.VeiPreco).HasPrecision(10, 2);
                entity.Property(e => e.VeiPrecoCompra).HasPrecision(10, 2);
                entity.Property(e => e.VeiSts).HasMaxLength(1);
                entity.Property(e => e.VeiSitSts).HasMaxLength(50);
                entity.Property(e => e.VeiObservacao).HasMaxLength(2000);
                entity.Property(e => e.VeiDonoAtual).HasMaxLength(150);
                entity.Property(e => e.VeiDonoCelular).HasMaxLength(20);
                entity.Property(e => e.VeiPrecoFipe).HasPrecision(10, 2);
                entity.HasOne(e => e.Loja).WithMany().HasForeignKey(e => e.R_LojId);
                entity.HasOne(e => e.Categoria).WithMany().HasForeignKey(e => e.R_CatId);
            });

            // Acesso
            modelBuilder.Entity<Acesso>(entity =>
            {
                entity.ToTable("Acesso");
                entity.HasKey(e => e.AcsId);
                entity.Property(e => e.AcsId).ValueGeneratedOnAdd();
                entity.Property(e => e.AcsNome).HasMaxLength(100).IsRequired();
                entity.Property(e => e.AcsDesc).HasMaxLength(255);
            });

            // Permissao
            modelBuilder.Entity<Permissao>(entity =>
            {
                entity.ToTable("Permissao");
                entity.HasKey(e => e.UsuAcsId);
                entity.Property(e => e.UsuAcsId).ValueGeneratedOnAdd();
                entity.Property(e => e.AcsTp).HasMaxLength(1);
                entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.R_UsuId);
                entity.HasOne(e => e.Acesso).WithMany().HasForeignKey(e => e.R_AcsId);
            });

            // Caracteristica
            modelBuilder.Entity<Caracteristica>(entity =>
            {
                entity.ToTable("Caracteristica");
                entity.HasKey(e => e.CarId);
                entity.Property(e => e.CarId).ValueGeneratedOnAdd();
                entity.Property(e => e.CarNome).HasMaxLength(100).IsRequired();
            });

            // VeiculoCaracteristica
            modelBuilder.Entity<VeiculoCaracteristica>(entity =>
            {
                entity.ToTable("VeiculoCaracteristica");
                entity.HasKey(e => e.VeiCarId);
                entity.Property(e => e.VeiCarId).ValueGeneratedOnAdd();
                entity.HasOne(e => e.Veiculo).WithMany(v => v.Caracteristicas).HasForeignKey(e => e.R_VeiId);
                entity.HasOne(e => e.Caracteristica).WithMany().HasForeignKey(e => e.R_CarId);
            });

            // Observacao
            modelBuilder.Entity<Observacao>(entity =>
            {
                entity.ToTable("Observacao");
                entity.HasKey(e => e.ObsId);
                entity.Property(e => e.ObsId).ValueGeneratedOnAdd();
                entity.Property(e => e.ObsNome).HasMaxLength(1000).IsRequired();
            });

            // VeiculoObservacao
            modelBuilder.Entity<VeiculoObservacao>(entity =>
            {
                entity.ToTable("VeiculoObservacao");
                entity.HasKey(e => e.VeiObsId);
                entity.Property(e => e.VeiObsId).ValueGeneratedOnAdd();
                entity.HasOne(e => e.Veiculo).WithMany(v => v.Observacoes).HasForeignKey(e => e.R_VeiId);
                entity.HasOne(e => e.Observacao).WithMany().HasForeignKey(e => e.R_ObsId);
            });

            // VeiculoImagem
            modelBuilder.Entity<VeiculoImagem>(entity =>
            {
                entity.ToTable("VeiculoImagem");
                entity.HasKey(e => e.ImgId);
                entity.Property(e => e.ImgId).ValueGeneratedOnAdd();
                entity.Property(e => e.ImgCaminho).HasMaxLength(500);
                entity.HasOne(e => e.Veiculo).WithMany(v => v.Imagens).HasForeignKey(e => e.R_VeiId);
            });

            // ConfiguracaoSistema
            modelBuilder.Entity<ConfiguracaoSistema>(entity =>
            {
                entity.ToTable("ConfiguracaoSistema");
                entity.HasKey(e => e.CfgId);
                entity.Property(e => e.CfgId).ValueGeneratedOnAdd();
                entity.Property(e => e.CfgChave).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CfgValor).HasMaxLength(2000);
                entity.HasIndex(e => e.CfgChave).IsUnique();
            });

            // VeiculoPublicacao
            modelBuilder.Entity<VeiculoPublicacao>(entity =>
            {
                entity.ToTable("VeiculoPublicacao");
                entity.HasKey(e => e.PubId);
                entity.Property(e => e.PubId).ValueGeneratedOnAdd();
                entity.Property(e => e.PubPlataforma).HasMaxLength(50).IsRequired();
                entity.Property(e => e.PubExternoId).HasMaxLength(200);
                entity.Property(e => e.PubStatus).HasMaxLength(20);
                entity.Property(e => e.PubUrl).HasMaxLength(500);
            });

            // Venda
            modelBuilder.Entity<Venda>(entity =>
            {
                entity.ToTable("Venda");
                entity.HasKey(e => e.VenId);
                entity.Property(e => e.VenId).ValueGeneratedOnAdd();
                entity.Property(e => e.VenMarca).HasMaxLength(100);
                entity.Property(e => e.VenModelo).HasMaxLength(150);
                entity.Property(e => e.VenChassi).HasMaxLength(20);
                entity.Property(e => e.VenValor).HasPrecision(10, 2);
                entity.Property(e => e.VenComissaoPorc).HasPrecision(5, 2);
                entity.Property(e => e.VenComissaoValor).HasPrecision(10, 2);
                // Dados do Comprador
                entity.Property(e => e.VenCompradorNome).HasMaxLength(200);
                entity.Property(e => e.VenCompradorCpf).HasMaxLength(14);
                entity.Property(e => e.VenCompradorTelefone).HasMaxLength(20);
                entity.Property(e => e.VenCompradorEmail).HasMaxLength(255);
                entity.Property(e => e.VenCompradorEndereco).HasMaxLength(500);
                // Forma de Pagamento e Status
                entity.Property(e => e.VenFormaPagamento).HasMaxLength(50);
                entity.Property(e => e.VenObservacao).HasMaxLength(1000);
                entity.Property(e => e.VenStatus).HasMaxLength(1);
                entity.HasOne(e => e.Veiculo).WithMany().HasForeignKey(e => e.R_VeiId);
                entity.HasOne(e => e.Vendedor).WithMany().HasForeignKey(e => e.R_UsuId);
            });

            // LogAuditoria
            modelBuilder.Entity<LogAuditoria>(entity =>
            {
                entity.ToTable("LogAuditoria");
                entity.HasKey(e => e.LogId);
                entity.Property(e => e.LogId).ValueGeneratedOnAdd();
                entity.Property(e => e.LogTabela).HasMaxLength(100).IsRequired();
                entity.Property(e => e.LogAcao).HasMaxLength(20).IsRequired();
                entity.Property(e => e.LogDadosAntigos);
                entity.Property(e => e.LogDadosNovos);
                entity.Property(e => e.LogUsuarioNome).HasMaxLength(200);
                entity.Property(e => e.LogIP).HasMaxLength(50);
                entity.Property(e => e.LogDataHora).IsRequired();
            });

            // HistoricoPreco
            modelBuilder.Entity<HistoricoPreco>(entity =>
            {
                entity.ToTable("HistoricoPreco");
                entity.HasKey(e => e.HisId);
                entity.Property(e => e.HisId).ValueGeneratedOnAdd();
                entity.Property(e => e.HisPrecoAnterior).HasPrecision(10, 2);
                entity.Property(e => e.HisPrecoNovo).HasPrecision(10, 2);
                entity.Property(e => e.HisMotivo).HasMaxLength(500);
                entity.HasOne(e => e.Veiculo).WithMany().HasForeignKey(e => e.R_VeiId);
                entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.R_UsuId);
            });

            // RefreshToken
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshToken");
                entity.HasKey(e => e.RefId);
                entity.Property(e => e.RefId).ValueGeneratedOnAdd();
                entity.Property(e => e.RefToken).HasMaxLength(100).IsRequired();
                entity.Property(e => e.RefJwtId).HasMaxLength(50);
                entity.Property(e => e.RefSubstituidoPor).HasMaxLength(100);
                entity.HasIndex(e => e.RefToken).IsUnique();
                entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.R_UsuId);
            });

            // Notificacao
            modelBuilder.Entity<Notificacao>(entity =>
            {
                entity.ToTable("Notificacao");
                entity.HasKey(e => e.NotId);
                entity.Property(e => e.NotId).ValueGeneratedOnAdd();
                entity.Property(e => e.NotTitulo).HasMaxLength(200).IsRequired();
                entity.Property(e => e.NotMensagem).HasMaxLength(1000);
                entity.Property(e => e.NotTipo).HasMaxLength(50);
                entity.Property(e => e.NotLink).HasMaxLength(500);
                entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.R_UsuId);
            });

            // Webhook
            modelBuilder.Entity<Webhook>(entity =>
            {
                entity.ToTable("Webhook");
                entity.HasKey(e => e.WebId);
                entity.Property(e => e.WebId).ValueGeneratedOnAdd();
                entity.Property(e => e.WebUrl).HasMaxLength(500).IsRequired();
                entity.Property(e => e.WebEventos).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.WebSecret).HasMaxLength(100).IsRequired();
            });

            // RecuperacaoSenha
            modelBuilder.Entity<RecuperacaoSenha>(entity =>
            {
                entity.ToTable("RecuperacaoSenha");
                entity.HasKey(e => e.RecId);
                entity.Property(e => e.RecId).ValueGeneratedOnAdd();
                entity.Property(e => e.RecToken).HasMaxLength(100).IsRequired();
            });

            // TestDrive
            modelBuilder.Entity<TestDrive>(entity => { entity.ToTable("TestDrive"); entity.HasKey(e => e.TdrId); entity.Property(e => e.TdrId).ValueGeneratedOnAdd(); entity.Property(e => e.TdrNomeCliente).HasMaxLength(200).IsRequired(); entity.Property(e => e.TdrTelefone).HasMaxLength(20); entity.Property(e => e.TdrWhatsApp).HasMaxLength(20); entity.Property(e => e.TdrEmail).HasMaxLength(255); entity.Property(e => e.TdrHorario).HasMaxLength(10); entity.Property(e => e.TdrObservacao).HasMaxLength(1000); entity.Property(e => e.TdrStatus).HasMaxLength(1); });

            // VeiculoDespesa
            modelBuilder.Entity<VeiculoDespesa>(entity => { entity.ToTable("VeiculoDespesa"); entity.HasKey(e => e.DesId); entity.Property(e => e.DesId).ValueGeneratedOnAdd(); entity.Property(e => e.DesTipo).HasMaxLength(50).IsRequired(); entity.Property(e => e.DesDescricao).HasMaxLength(500); entity.Property(e => e.DesValor).HasPrecision(10, 2); });

            // Lead
            modelBuilder.Entity<Lead>(entity => { entity.ToTable("Lead"); entity.HasKey(e => e.LeaId); entity.Property(e => e.LeaId).ValueGeneratedOnAdd(); entity.Property(e => e.LeaNomeCliente).HasMaxLength(200); entity.Property(e => e.LeaTelefone).HasMaxLength(20); entity.Property(e => e.LeaEmail).HasMaxLength(255); entity.Property(e => e.LeaOrigem).HasMaxLength(30); entity.Property(e => e.LeaStatus).HasMaxLength(20); entity.Property(e => e.LeaObservacao).HasMaxLength(1000); entity.Property(e => e.LeaCpf).HasMaxLength(14); entity.Property(e => e.LeaRenda).HasPrecision(12, 2); entity.Property(e => e.LeaEntrada).HasPrecision(12, 2); });

            // Negociacao
            modelBuilder.Entity<Negociacao>(entity => { entity.ToTable("Negociacao"); entity.HasKey(e => e.NegId); entity.Property(e => e.NegId).ValueGeneratedOnAdd(); entity.Property(e => e.NegNomeCliente).HasMaxLength(200); entity.Property(e => e.NegTelefone).HasMaxLength(20); entity.Property(e => e.NegEmail).HasMaxLength(255); entity.Property(e => e.NegValorProposta).HasPrecision(10, 2); entity.Property(e => e.NegStatus).HasMaxLength(20); entity.Property(e => e.NegObservacao).HasMaxLength(1000); });

            // Favorito
            modelBuilder.Entity<Favorito>(entity => { entity.ToTable("Favorito"); entity.HasKey(e => e.FavId); entity.Property(e => e.FavId).ValueGeneratedOnAdd(); entity.Property(e => e.FavEmail).HasMaxLength(255).IsRequired(); entity.Property(e => e.FavNome).HasMaxLength(200); entity.Property(e => e.FavTelefone).HasMaxLength(20); entity.HasIndex(e => new { e.FavEmail, e.R_VeiId }).IsUnique(); });

            // VeiculoDocumento
            modelBuilder.Entity<VeiculoDocumento>(entity =>
            {
                entity.ToTable("VeiculoDocumento");
                entity.HasKey(e => e.DocId);
                entity.Property(e => e.DocId).ValueGeneratedOnAdd();
                entity.Property(e => e.DocTipo).HasMaxLength(50).IsRequired();
                entity.Property(e => e.DocStatus).HasMaxLength(20).IsRequired();
                entity.Property(e => e.DocArquivo).HasMaxLength(500);
                entity.Property(e => e.DocObservacao).HasMaxLength(1000);
            });

            // PushSubscription
            modelBuilder.Entity<PushSubscription>(entity =>
            {
                entity.ToTable("PushSubscription");
                entity.HasKey(e => e.PsbId);
                entity.Property(e => e.PsbId).ValueGeneratedOnAdd();
                entity.Property(e => e.PsbEndpoint).HasMaxLength(500).IsRequired();
                entity.Property(e => e.PsbP256dh).HasMaxLength(200).IsRequired();
                entity.Property(e => e.PsbAuth).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PsbUserAgent).HasMaxLength(500);
                entity.HasIndex(e => e.PsbEndpoint).IsUnique();
            });
        }
    }
}
