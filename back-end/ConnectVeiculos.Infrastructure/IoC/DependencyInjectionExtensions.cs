using ConnectVeiculos.Application.Interfaces.Acessos;
using ConnectVeiculos.Application.Interfaces.Auth;
using ConnectVeiculos.Application.Interfaces.Catalogo;
using ConnectVeiculos.Application.Interfaces.Categorias;
using ConnectVeiculos.Application.Interfaces.Dashboard;
using ConnectVeiculos.Application.Interfaces.Imagens;
using ConnectVeiculos.Application.Interfaces.Lojas;
using ConnectVeiculos.Application.Interfaces.RecuperacaoSenha;
using ConnectVeiculos.Application.Interfaces.Relatorios;
using ConnectVeiculos.Application.Interfaces.Usuarios;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Application.Interfaces.Vendas;
using ConnectVeiculos.Infrastructure.Cache;
using ConnectVeiculos.Infrastructure.Email;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Application.UseCases.Acessos;
using ConnectVeiculos.Application.UseCases.Auth;
using ConnectVeiculos.Application.UseCases.Catalogo;
using ConnectVeiculos.Application.UseCases.Categorias;
using ConnectVeiculos.Application.UseCases.Dashboard;
using ConnectVeiculos.Application.UseCases.Imagens;
using ConnectVeiculos.Application.UseCases.Lojas;
using ConnectVeiculos.Application.UseCases.RecuperacaoSenha;
using ConnectVeiculos.Application.UseCases.Relatorios;
using ConnectVeiculos.Application.UseCases.Usuarios;
using ConnectVeiculos.Application.UseCases.Veiculos;
using ConnectVeiculos.Application.UseCases.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Caracteristicas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.HistoricoPrecos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Observacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.RefreshTokens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Logs;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Webhooks;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.Webhook;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories;
using ConnectVeiculos.Infrastructure.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Infrastructure.Database.Operations.Usuarios;
using ConnectVeiculos.Infrastructure.Database.Operations.Veiculos;
using ConnectVeiculos.Infrastructure.Database.UnitOfWork;
using ConnectVeiculos.Infrastructure.Database.Interceptors;
using ConnectVeiculos.Infrastructure.Hubs;
using ConnectVeiculos.Infrastructure.Middlewares;
using ConnectVeiculos.Infrastructure.Services.Financiamento;
using ConnectVeiculos.Infrastructure.Services.Fipe;
using ConnectVeiculos.Infrastructure.Services.Imagem;
using ConnectVeiculos.Infrastructure.Services.QrCode;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectVeiculos.Infrastructure.IoC
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection WireUpDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            // Verificar se há variável de ambiente para PostgreSQL (produção)
            var postgresConnection = Environment.GetEnvironmentVariable("DATABASE_URL");
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=ConnectVeiculos.db";

            var usePostgres = !string.IsNullOrEmpty(postgresConnection);

            // Cache In-Memory
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();

            // Email Service
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.AddTransient<IEmailService, SmtpEmailService>();

            // DbSession para Dapper (usa a connection string apropriada)
            var dapperConnectionString = usePostgres ? postgresConnection! : connectionString;
            services.AddScoped(sp => new DbSession(dapperConnectionString, usePostgres));
            services.AddScoped<IUnitOfWork, Database.UnitOfWork.UnitOfWork>();

            // Registrar interceptors
            services.AddSingleton<SoftDeleteInterceptor>();
            services.AddScoped<AuditInterceptor>();

            // DbContext para Entity Framework (PostgreSQL em produção, SQLite em desenvolvimento)
            services.AddDbContext<ConnectVeiculosDbContext>((serviceProvider, options) =>
            {
                var softDeleteInterceptor = serviceProvider.GetRequiredService<SoftDeleteInterceptor>();
                var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();

                if (usePostgres)
                {
                    options.UseNpgsql(postgresConnection);
                }
                else
                {
                    options.UseSqlite(connectionString);
                }

                options.AddInterceptors(softDeleteInterceptor, auditInterceptor);
            });

            // Repositories
            services.AddTransient<IUsuarioRepository, UsuarioRepository>();
            services.AddTransient<IVeiculoRepository, VeiculoRepository>();
            services.AddTransient<ILojaRepository, LojaRepository>();
            services.AddTransient<ICategoriaRepository, CategoriaRepository>();
            services.AddTransient<IAcessoRepository, AcessoRepository>();
            services.AddTransient<IPermissaoRepository, PermissaoRepository>();
            services.AddTransient<ILojaUsuarioRepository, LojaUsuarioRepository>();
            services.AddTransient<ICaracteristicaRepository, CaracteristicaRepository>();
            services.AddTransient<IObservacaoRepository, ObservacaoRepository>();
            services.AddTransient<IVeiculoImagemRepository, VeiculoImagemRepository>();
            services.AddTransient<IVendaRepository, VendaRepository>();
            services.AddTransient<ILogAuditoriaRepository, LogAuditoriaRepository>();
            services.AddTransient<IHistoricoPrecoRepository, HistoricoPrecoRepository>();
            services.AddTransient<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddTransient<INotificacaoRepository, NotificacaoRepository>();
            services.AddTransient<IWebhookRepository, WebhookRepository>();

            // Services
            services.AddTransient<IQrCodeService, QrCodeService>();
            services.AddTransient<IImagemService, ImagemService>();
            services.AddTransient<IFinanciamentoService, FinanciamentoService>();
            services.AddHttpClient<IFipeService, FipeService>();
            services.AddHttpClient<IWebhookService, WebhookService>();

            // SignalR Hub Services
            services.AddSingleton<NotificacaoHubService>();
            services.AddSingleton<INotificacaoHubService>(sp => sp.GetRequiredService<NotificacaoHubService>());
            services.AddSingleton<INotificacaoService>(sp => sp.GetRequiredService<NotificacaoHubService>());
            services.AddSingleton<ICatalogoHubService, CatalogoHubService>();

            // Operations
            services.AddTransient<IUsuarioOperations, UsuarioOperations>();
            services.AddTransient<IVeiculoOperations, VeiculoOperations>();
            services.AddTransient<IRecuperacaoSenhaOperations, RecuperacaoSenhaOperations>();

            // UseCases - Auth
            services.AddTransient<ILoginUseCase, LoginUseCase>();

            // UseCases - Recuperacao de Senha
            services.AddTransient<ISolicitarRecuperacaoSenhaUseCase, SolicitarRecuperacaoSenhaUseCase>();
            services.AddTransient<IRedefinirSenhaUseCase, RedefinirSenhaUseCase>();

            // UseCases - Usuarios
            services.AddTransient<IConsultarUsuariosUseCase, ConsultarUsuariosUseCase>();
            services.AddTransient<IConsultarUsuariosPaginadoUseCase, ConsultarUsuariosPaginadoUseCase>();
            services.AddTransient<IConsultarUsuarioPorIdUseCase, ConsultarUsuarioPorIdUseCase>();
            services.AddTransient<ICadastrarUsuarioUseCase, CadastrarUsuarioUseCase>();
            services.AddTransient<IAtualizarUsuarioUseCase, AtualizarUsuarioUseCase>();
            services.AddTransient<IInativarUsuarioUseCase, InativarUsuarioUseCase>();

            // UseCases - Veiculos
            services.AddTransient<IConsultarVeiculosUseCase, ConsultarVeiculosUseCase>();
            services.AddTransient<IConsultarVeiculosPaginadoUseCase, ConsultarVeiculosPaginadoUseCase>();
            services.AddTransient<IConsultarVeiculoPorIdUseCase, ConsultarVeiculoPorIdUseCase>();
            services.AddTransient<ICadastrarVeiculoUseCase, CadastrarVeiculoUseCase>();
            services.AddTransient<IAtualizarVeiculoUseCase, AtualizarVeiculoUseCase>();
            services.AddTransient<IInativarVeiculoUseCase, InativarVeiculoUseCase>();
            services.AddTransient<IImportarVeiculosUseCase, ImportarVeiculosUseCase>();
            services.AddTransient<IBuscaAvancadaVeiculosUseCase, BuscaAvancadaVeiculosUseCase>();

            // UseCases - Lojas
            services.AddTransient<IConsultarLojasUseCase, ConsultarLojasUseCase>();
            services.AddTransient<IConsultarLojasPaginadoUseCase, ConsultarLojasPaginadoUseCase>();
            services.AddTransient<IConsultarLojaPorIdUseCase, ConsultarLojaPorIdUseCase>();
            services.AddTransient<ICadastrarLojaUseCase, CadastrarLojaUseCase>();
            services.AddTransient<IAtualizarLojaUseCase, AtualizarLojaUseCase>();
            services.AddTransient<IInativarLojaUseCase, InativarLojaUseCase>();

            // UseCases - Acessos
            services.AddTransient<IConsultarAcessosUseCase, ConsultarAcessosUseCase>();
            services.AddTransient<IConsultarAcessosPaginadoUseCase, ConsultarAcessosPaginadoUseCase>();
            services.AddTransient<IConsultarAcessoPorIdUseCase, ConsultarAcessoPorIdUseCase>();
            services.AddTransient<ICadastrarAcessoUseCase, CadastrarAcessoUseCase>();
            services.AddTransient<IAtualizarAcessoUseCase, AtualizarAcessoUseCase>();
            services.AddTransient<IInativarAcessoUseCase, InativarAcessoUseCase>();

            // UseCases - Categorias
            services.AddTransient<IConsultarCategoriasUseCase, ConsultarCategoriasUseCase>();
            services.AddTransient<IConsultarCategoriasPaginadoUseCase, ConsultarCategoriasPaginadoUseCase>();
            services.AddTransient<IConsultarCategoriaPorIdUseCase, ConsultarCategoriaPorIdUseCase>();
            services.AddTransient<ICadastrarCategoriaUseCase, CadastrarCategoriaUseCase>();
            services.AddTransient<IAtualizarCategoriaUseCase, AtualizarCategoriaUseCase>();
            services.AddTransient<IInativarCategoriaUseCase, InativarCategoriaUseCase>();

            // UseCases - Catalogo (Publico)
            services.AddTransient<IConsultarCatalogoUseCase, ConsultarCatalogoUseCase>();

            // UseCases - Dashboard
            services.AddTransient<IConsultarDashboardUseCase, ConsultarDashboardUseCase>();
            services.AddTransient<IConsultarVendasPorPeriodoUseCase, ConsultarVendasPorPeriodoUseCase>();
            services.AddTransient<IConsultarFaturamentoMensalUseCase, ConsultarFaturamentoMensalUseCase>();
            services.AddTransient<IConsultarTopVeiculosUseCase, ConsultarTopVeiculosUseCase>();
            services.AddTransient<IConsultarComparativoMensalUseCase, ConsultarComparativoMensalUseCase>();

            // UseCases - Vendas
            services.AddTransient<IConsultarVendasUseCase, ConsultarVendasUseCase>();
            services.AddTransient<IConsultarVendaPorIdUseCase, ConsultarVendaPorIdUseCase>();
            services.AddTransient<IRegistrarVendaUseCase, RegistrarVendaUseCase>();
            services.AddTransient<IEstornarVendaUseCase, EstornarVendaUseCase>();

            // UseCases - Imagens
            services.AddTransient<IConsultarImagensVeiculoUseCase, ConsultarImagensVeiculoUseCase>();
            services.AddTransient<IUploadImagemVeiculoUseCase, UploadImagemVeiculoUseCase>();
            services.AddTransient<IExcluirImagemVeiculoUseCase, ExcluirImagemVeiculoUseCase>();

            // UseCases - Relatorios
            services.AddTransient<IConsultarRelatorioVendasUseCase, ConsultarRelatorioVendasUseCase>();
            services.AddTransient<IConsultarRelatorioEstoqueUseCase, ConsultarRelatorioEstoqueUseCase>();
            services.AddTransient<IConsultarRelatorioFinanceiroUseCase, ConsultarRelatorioFinanceiroUseCase>();

            return services;
        }

        public static IApplicationBuilder UseErrorHandlingMiddleware(this IApplicationBuilder app) =>
            app.UseMiddleware<ErrorHandlingMiddleware>();

        public static IApplicationBuilder UseInitializeDatabase(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConnectVeiculosDbContext>();
            dbContext.Database.EnsureCreated();

            // Aplicar alteracoes de schema para bancos existentes
            ApplySchemaUpdates(dbContext);

            // Seed inicial - criar usuario admin se nao existir
            SeedInitialData(dbContext);

            return app;
        }

        private static void ApplySchemaUpdates(ConnectVeiculosDbContext dbContext)
        {
            try
            {
                var connection = dbContext.Database.GetDbConnection();
                connection.Open();

                // Adicionar coluna VeiObservacao na tabela Veiculo
                AddColumnIfNotExists(connection, "Veiculo", "VeiObservacao", "TEXT");

                // Adicionar coluna LojWhatsApp na tabela Loja
                AddColumnIfNotExists(connection, "Loja", "LojWhatsApp", "TEXT");

                // Adicionar colunas de personalizacao de cores
                AddColumnIfNotExists(connection, "Loja", "LojCorPrimaria", "TEXT");
                AddColumnIfNotExists(connection, "Loja", "LojCorSecundaria", "TEXT");

                // Adicionar colunas de redes sociais na Loja
                AddColumnIfNotExists(connection, "Loja", "LojInstagram", "TEXT");
                AddColumnIfNotExists(connection, "Loja", "LojFacebook", "TEXT");

                // Adicionar coluna LojSlug na tabela Loja
                AddColumnIfNotExists(connection, "Loja", "LojSlug", "TEXT");

                // Adicionar colunas de status de postagem no Veiculo
                AddColumnIfNotExists(connection, "Veiculo", "VeiPostadoInsta", "INTEGER DEFAULT 0");
                AddColumnIfNotExists(connection, "Veiculo", "VeiPostadoFace", "INTEGER DEFAULT 0");
                AddColumnIfNotExists(connection, "Veiculo", "VeiDtPostagemInsta", "TEXT");
                AddColumnIfNotExists(connection, "Veiculo", "VeiDtPostagemFace", "TEXT");

                // Adicionar coluna WhatsApp no TestDrive
                AddColumnIfNotExists(connection, "TestDrive", "TdrWhatsApp", "TEXT");

                // Adicionar colunas de dono atual do veiculo
                AddColumnIfNotExists(connection, "Veiculo", "VeiDonoAtual", "TEXT");
                AddColumnIfNotExists(connection, "Veiculo", "VeiDonoCelular", "TEXT");

                // Criar tabelas que podem nao existir em bancos antigos
                CreateTableIfNotExists(connection, "Lead",
                    @"LeaId INTEGER PRIMARY KEY AUTOINCREMENT,
                      R_VeiId INTEGER,
                      R_LojId INTEGER,
                      LeaNomeCliente TEXT,
                      LeaTelefone TEXT,
                      LeaEmail TEXT,
                      LeaOrigem TEXT,
                      LeaStatus TEXT,
                      LeaObservacao TEXT,
                      LeaDtCriacao TEXT NOT NULL DEFAULT (datetime('now'))");

                CreateTableIfNotExists(connection, "TestDrive",
                    @"TdrId INTEGER PRIMARY KEY AUTOINCREMENT,
                      R_VeiId INTEGER,
                      R_LojId INTEGER,
                      TdrNomeCliente TEXT NOT NULL,
                      TdrTelefone TEXT,
                      TdrEmail TEXT,
                      TdrDataAgendamento TEXT NOT NULL,
                      TdrHorario TEXT,
                      TdrObservacao TEXT,
                      TdrStatus TEXT,
                      TdrDtCriacao TEXT NOT NULL DEFAULT (datetime('now'))");

                CreateTableIfNotExists(connection, "VeiculoDespesa",
                    @"DesId INTEGER PRIMARY KEY AUTOINCREMENT,
                      R_VeiId INTEGER NOT NULL,
                      DesTipo TEXT NOT NULL,
                      DesDescricao TEXT,
                      DesValor REAL,
                      DesDtCriacao TEXT NOT NULL DEFAULT (datetime('now'))");

                CreateTableIfNotExists(connection, "RecuperacaoSenha",
                    @"RecId INTEGER PRIMARY KEY AUTOINCREMENT,
                      RecUsuId INTEGER NOT NULL,
                      RecToken TEXT NOT NULL,
                      RecDataCriacao TEXT,
                      RecDataExpiracao TEXT,
                      RecUtilizado INTEGER DEFAULT 0");

                CreateTableIfNotExists(connection, "Favorito",
                    @"FavId INTEGER PRIMARY KEY AUTOINCREMENT,
                      R_VeiId INTEGER NOT NULL,
                      FavEmail TEXT NOT NULL,
                      FavNome TEXT,
                      FavTelefone TEXT,
                      FavDtCriacao TEXT NOT NULL DEFAULT (datetime('now'))");

                CreateTableIfNotExists(connection, "Negociacao",
                    @"NegId INTEGER PRIMARY KEY AUTOINCREMENT,
                      R_VeiId INTEGER NOT NULL,
                      R_LojId INTEGER,
                      NegNomeCliente TEXT,
                      NegTelefone TEXT,
                      NegEmail TEXT,
                      NegValorProposta REAL,
                      NegStatus TEXT,
                      NegObservacao TEXT,
                      NegDtCriacao TEXT NOT NULL DEFAULT (datetime('now'))");

                // Populate slugs for existing lojas
                using var slugCmd = connection.CreateCommand();
                slugCmd.CommandText = "SELECT LojId, LojNome FROM Loja WHERE LojSlug IS NULL OR LojSlug = ''";
                var reader = slugCmd.ExecuteReader();
                var updates = new List<(int id, string slug)>();
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var nome = reader.GetString(1);
                    var slug = GenerateSlug(nome);
                    updates.Add((id, slug));
                }
                reader.Close();
                foreach (var (id, slug) in updates)
                {
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = $"UPDATE Loja SET LojSlug = '{slug}' WHERE LojId = {id}";
                    updateCmd.ExecuteNonQuery();
                }

                connection.Close();
            }
            catch
            {
                // Ignora erros de schema (coluna pode ja existir ou banco novo)
            }
        }

        private static void CreateTableIfNotExists(System.Data.Common.DbConnection connection, string tableName, string columns)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName} ({columns})";
            cmd.ExecuteNonQuery();
        }

        private static void AddColumnIfNotExists(System.Data.Common.DbConnection connection, string tableName, string columnName, string columnType)
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'";
            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            if (!exists)
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}";
                alterCmd.ExecuteNonQuery();
            }
        }

        private static string GenerateSlug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var slug = text.ToLower()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
                .Replace("ã", "a").Replace("õ", "o").Replace("â", "a").Replace("ê", "e").Replace("ô", "o")
                .Replace("ç", "c");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[\s-]+", "-").Trim('-');
            return slug;
        }

        private static void SeedInitialData(ConnectVeiculosDbContext dbContext)
        {
            // Seed de niveis de acesso
            if (!dbContext.Acessos.Any())
            {
                var acessos = new[]
                {
                    new Core.Entities.Acessos.Acesso(0, "Administrador", "Acesso total ao sistema, gerencia usuarios, lojas e configuracoes", true),
                    new Core.Entities.Acessos.Acesso(0, "Gerente", "Gerencia veiculos, vendas, relatorios e equipe da loja", true),
                    new Core.Entities.Acessos.Acesso(0, "Vendedor", "Cadastra e edita veiculos, registra vendas e atende leads", true),
                    new Core.Entities.Acessos.Acesso(0, "Visualizador", "Acesso somente leitura, consulta veiculos, vendas e relatorios", true)
                };
                dbContext.Acessos.AddRange(acessos);
                dbContext.SaveChanges();
            }

            // Verificar se já existe usuario admin
            if (!dbContext.Usuarios.Any(u => u.UsuEmail == "admin@connectveiculos.com.br"))
            {
                var adminUser = new Core.Entities.Usuarios.Usuario(
                    0,
                    "Administrador",
                    "",
                    "",
                    "admin@connectveiculos.com.br",
                    BCrypt.Net.BCrypt.HashPassword("admin123"),
                    "Administrador",
                    true
                );
                dbContext.Usuarios.Add(adminUser);
                dbContext.SaveChanges();
            }
        }
    }
}
