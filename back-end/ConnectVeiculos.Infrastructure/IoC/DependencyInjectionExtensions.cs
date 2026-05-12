using ConnectVeiculos.Application.Interfaces.Acessos;
using ConnectVeiculos.Application.Interfaces.Auth;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Tenancy;
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

            // Cache In-Memory.
            // MemoryCacheService eh Singleton (cache de processo). Mas o service
            // exposto via ICacheService eh o decorator TenantAwareCacheService
            // (Scoped), que prefixa chaves com "tenant:{id}:" automaticamente
            // quando ha tenant resolvido. Sem isso, dois tenants compartilhariam
            // chaves identicas (ex: "dashboard") e haveria vazamento.
            services.AddMemoryCache();
            services.AddSingleton<MemoryCacheService>();
            services.AddScoped<ICacheService, TenantAwareCacheService>();

            // ===== Tenancy infrastructure (criada na Fase 2 do multi-tenant)
            // Componentes registrados aqui mas o middleware ainda nao esta ativo
            // no pipeline (ver Program.cs). Sistema continua single-tenant ate
            // a Fase 5 ativar o middleware e migrar o banco atual para tenant
            // "default".
            //
            // Master DbContext — registry de tenants em data/_master.db
            var dataDir = Environment.GetEnvironmentVariable("TENANTS_DATA_DIR") ?? "/app/data";
            var masterDbPath = Path.Combine(dataDir, "_master.db");
            services.AddDbContext<MasterDbContext>(options =>
                options.UseSqlite($"Data Source={masterDbPath}"));
            services.AddSingleton<ITenantStore, TenantStore>();
            services.AddScoped<ITenantContext, TenantContext>();
            services.AddScoped<ITenantConnectionFactory, TenantConnectionFactory>();
            services.AddSingleton<TenantsMigrationsRunner>();
            // ===== fim tenancy

            // Email Service
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.AddTransient<IEmailService, SmtpEmailService>();
            services.AddTransient<Core.Interfaces.Services.IFavoritoNotificacaoService, Services.Notificacao.FavoritoNotificacaoService>();
            services.AddTransient<Core.Interfaces.Services.IPushNotificationService, Services.Push.PushNotificationService>();
            services.AddHttpClient<Core.Interfaces.Services.IWhatsAppService, Services.WhatsApp.WhatsAppService>()
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

            // DbSession para Dapper — agora tenant-aware via TenantConnectionFactory.
            // Em request HTTP com tenant resolvido, usa data/{slug}.db; senao
            // (startup, jobs, testes) usa a connection string padrao do appsettings.
            services.AddScoped(sp =>
            {
                var factory = sp.GetRequiredService<ITenantConnectionFactory>();
                return new DbSession(factory.GetConnectionString(), usePostgres);
            });
            services.AddScoped<IUnitOfWork, Database.UnitOfWork.UnitOfWork>();

            // Registrar interceptors
            services.AddSingleton<SoftDeleteInterceptor>();
            services.AddScoped<AuditInterceptor>();

            // DbContext para Entity Framework — tambem tenant-aware via factory.
            // Mesma logica do DbSession acima: tenant resolvido na request usa
            // banco do tenant; sem tenant resolvido cai no fallback.
            services.AddDbContext<ConnectVeiculosDbContext>((serviceProvider, options) =>
            {
                var softDeleteInterceptor = serviceProvider.GetRequiredService<SoftDeleteInterceptor>();
                var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
                var tenantFactory = serviceProvider.GetRequiredService<ITenantConnectionFactory>();
                var connStr = tenantFactory.GetConnectionString();

                if (usePostgres)
                {
                    options.UseNpgsql(connStr);
                }
                else
                {
                    options.UseSqlite(connStr);
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
            services.AddTransient<Core.Interfaces.Database.Repositories.Despesas.IVeiculoDespesaRepository, Database.EntityFramework.Repositories.VeiculoDespesaRepository>();

            // Services
            services.AddTransient<IQrCodeService, QrCodeService>();
            services.AddTransient<IImagemService, ImagemService>();
            services.AddTransient<IFinanciamentoService, FinanciamentoService>();
            services.AddHttpClient<IFipeService, FipeService>();
            services.AddHttpClient<IWebhookService, WebhookService>();

            // SignalR Hub Services
            // NotificacaoHubService agora eh Scoped — depende de ITenantContext (Scoped).
            // IHubContext<T> continua Singleton internamente no SignalR; o servico
            // wrapper apenas o usa pra broadcast. Cada request/job-scope cria a sua
            // instancia com tenant context resolvido.
            services.AddScoped<NotificacaoHubService>();
            services.AddScoped<INotificacaoHubService>(sp => sp.GetRequiredService<NotificacaoHubService>());
            services.AddScoped<INotificacaoService>(sp => sp.GetRequiredService<NotificacaoHubService>());
            services.AddSingleton<ICatalogoHubService, CatalogoHubService>();

            // Operations
            services.AddTransient<IUsuarioOperations, UsuarioOperations>();
            services.AddTransient<IVeiculoOperations, VeiculoOperations>();
            services.AddTransient<IRecuperacaoSenhaOperations, RecuperacaoSenhaOperations>();

            // UseCases - Auth
            services.AddTransient<ILoginUseCase, LoginUseCase>();
            services.AddTransient<ITrocarSenhaUseCase, TrocarSenhaUseCase>();

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
            services.AddTransient<IExcluirLojaUseCase, ExcluirLojaUseCase>();

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
            services.AddTransient<IConsultarLucroDashboardUseCase, ConsultarLucroDashboardUseCase>();

            // UseCases - Vendas
            services.AddTransient<IConsultarVendasUseCase, ConsultarVendasUseCase>();
            services.AddTransient<IConsultarVendaPorIdUseCase, ConsultarVendaPorIdUseCase>();
            services.AddTransient<IRegistrarVendaUseCase, RegistrarVendaUseCase>();
            services.AddTransient<IEstornarVendaUseCase, EstornarVendaUseCase>();

            // Repositories - Configuracoes
            services.AddTransient<Core.Interfaces.Database.Repositories.Configuracoes.IConfiguracaoSistemaRepository, Database.EntityFramework.Repositories.ConfiguracaoSistemaRepository>();

            // Repositories - Publicacoes
            services.AddTransient<Core.Interfaces.Database.Repositories.Publicacoes.IVeiculoPublicacaoRepository, Database.EntityFramework.Repositories.VeiculoPublicacaoRepository>();

            // Services - Integracoes
            services.Configure<Services.MercadoLivre.MercadoLivreSettings>(opts =>
            {
                configuration.GetSection("MercadoLivreSettings").Bind(opts);
                // Override via env vars (prioritarias para producao)
                var envAppId = Environment.GetEnvironmentVariable("ML_APP_ID");
                var envSecret = Environment.GetEnvironmentVariable("ML_CLIENT_SECRET");
                var envRedirect = Environment.GetEnvironmentVariable("ML_REDIRECT_URI");
                if (!string.IsNullOrEmpty(envAppId)) opts.AppId = envAppId;
                if (!string.IsNullOrEmpty(envSecret)) opts.ClientSecret = envSecret;
                if (!string.IsNullOrEmpty(envRedirect)) opts.RedirectUri = envRedirect;
            });
            services.AddHttpClient<Core.Interfaces.Services.IMercadoLivreService, Services.MercadoLivre.MercadoLivreService>()
                .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10));
            services.AddTransient<Core.Interfaces.Services.IFeedService, Services.Feed.FeedService>();

            // Services - Facebook Catalog (push instantaneo)
            services.Configure<Services.Facebook.FacebookCatalogSettings>(configuration.GetSection("FacebookCatalogSettings"));
            services.AddHttpClient<Core.Interfaces.Services.IFacebookCatalogService, Services.Facebook.FacebookCatalogService>()
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

            // Services - Google Merchant (push instantaneo)
            services.Configure<Services.Google.GoogleMerchantSettings>(configuration.GetSection("GoogleMerchantSettings"));
            services.AddHttpClient<Core.Interfaces.Services.IGoogleMerchantService, Services.Google.GoogleMerchantService>()
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

            // Services - Financiamento Bancos
            services.Configure<Services.Financiamento.BvFinanciamentoSettings>(configuration.GetSection("BvFinanciamentoSettings"));
            services.Configure<Services.Financiamento.PanFinanciamentoSettings>(configuration.GetSection("PanFinanciamentoSettings"));
            services.AddHttpClient<Core.Interfaces.Services.IBancoFinanciamentoService, Services.Financiamento.BvFinanciamentoService>("BV")
                .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15));
            services.AddHttpClient<Core.Interfaces.Services.IBancoFinanciamentoService, Services.Financiamento.PanFinanciamentoService>("PAN")
                .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15));

            // UseCases - Imagens
            services.AddTransient<IConsultarImagensVeiculoUseCase, ConsultarImagensVeiculoUseCase>();
            services.AddTransient<IUploadImagemVeiculoUseCase, UploadImagemVeiculoUseCase>();
            services.AddTransient<IExcluirImagemVeiculoUseCase, ExcluirImagemVeiculoUseCase>();
            services.AddTransient<IDefinirImagemPrincipalUseCase, DefinirImagemPrincipalUseCase>();

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

            // Seed inicial — admin + Acessos + Categorias.
            // ApplySchemaUpdates NAO eh chamado aqui mais — o TenantsMigrationsRunner
            // roda em todos os tenants ativos (incluindo default) logo depois e aplica
            // os schema updates em cada banco.
            SeedInitialData(dbContext);

            return app;
        }

        /// <summary>
        /// Aplica alteracoes de schema (AddColumnIfNotExists / CreateTableIfNotExists)
        /// em qualquer DbContext do tipo ConnectVeiculosDbContext. Idempotente — pode
        /// rodar quantas vezes quiser, so adiciona o que faltar. Chamado no startup
        /// pelo TenantsMigrationsRunner para cada tenant ativo, e tambem pelo
        /// TenantsAdminController quando cria tenant novo.
        /// </summary>
        public static void ApplySchemaUpdates(ConnectVeiculosDbContext dbContext)
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

                // Adicionar coluna de opcionais do veiculo
                AddColumnIfNotExists(connection, "Veiculo", "VeiOpcionais", "TEXT");

                // URL do catalogo compartilhada entre lojas
                AddColumnIfNotExists(connection, "Loja", "LojUrlCatalogo", "TEXT");

                // Preco FIPE do veiculo (consulta automatica/manual)
                AddColumnIfNotExists(connection, "Veiculo", "VeiPrecoFipe", "REAL");

                // Limpa strings vazias em colunas decimais nullable. SQLite armazena
                // decimal como TEXT, e EF Core lanca FormatException ao tentar
                // decimal.Parse(''). Idempotente — converte '' para NULL para evitar o erro.
                NormalizeEmptyDecimal(connection, "Veiculo", "VeiPrecoFipe");

                // VeiPreco e VeiPrecoCompra sao NOT NULL decimal mas SQL legacy/seeds
                // podem ter inserido '' (TEXT vazio). EF Core falha ao ler.
                // Para colunas NOT NULL, '' nao pode virar NULL → seta '0'.
                NormalizeEmptyDecimalNotNull(connection, "Veiculo", "VeiPreco");
                NormalizeEmptyDecimalNotNull(connection, "Veiculo", "VeiPrecoCompra");

                // Favorito: UNIQUE (email, veiculo) — bloqueia favoritos duplicados.
                // Idempotente. SQLite cria indice composto se nao existir.
                using (var idxCmd = connection.CreateCommand())
                {
                    idxCmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Favorito_Email_Veiculo ON Favorito(FavEmail, R_VeiId)";
                    try { idxCmd.ExecuteNonQuery(); }
                    catch { /* tabela ainda nao existe em bancos novos antes do EnsureCreated; ignora */ }
                }

                // Campos de financiamento no Lead
                AddColumnIfNotExists(connection, "Lead", "LeaCpf", "TEXT");
                AddColumnIfNotExists(connection, "Lead", "LeaRenda", "REAL");
                AddColumnIfNotExists(connection, "Lead", "LeaEntrada", "REAL");
                AddColumnIfNotExists(connection, "Lead", "LeaParcelas", "INTEGER");

                // Tabela de configuracoes do sistema (tokens ML/Google/Facebook etc.)
                CreateTableIfNotExists(connection, "ConfiguracaoSistema",
                    @"CfgId INTEGER PRIMARY KEY AUTOINCREMENT,
                      CfgChave TEXT NOT NULL UNIQUE,
                      CfgValor TEXT,
                      CfgDtAtualizacao TEXT");

                // Tabela de publicacoes em plataformas externas
                CreateTableIfNotExists(connection, "VeiculoPublicacao",
                    @"PubId INTEGER PRIMARY KEY AUTOINCREMENT,
                      R_VeiId INTEGER NOT NULL,
                      PubPlataforma TEXT NOT NULL,
                      PubExternoId TEXT,
                      PubStatus TEXT DEFAULT 'ATIVO',
                      PubUrl TEXT,
                      PubDtPublicacao TEXT,
                      PubDtRemocao TEXT");

                // Tabela de documentos do veiculo (CRLV, laudo, transferencia, etc)
                CreateTableIfNotExists(connection, "VeiculoDocumento",
                    @"DocId INTEGER PRIMARY KEY AUTOINCREMENT,
                      R_VeiId INTEGER NOT NULL,
                      DocTipo TEXT NOT NULL,
                      DocStatus TEXT NOT NULL DEFAULT 'PENDENTE',
                      DocArquivo TEXT,
                      DocObservacao TEXT,
                      DocDtVencimento TEXT,
                      DocDtCriacao TEXT NOT NULL DEFAULT (datetime('now')),
                      DocDtConclusao TEXT");

                // Tabela de subscriptions de Push Notification (PWA)
                CreateTableIfNotExists(connection, "PushSubscription",
                    @"PsbId INTEGER PRIMARY KEY AUTOINCREMENT,
                      R_UsuId INTEGER,
                      PsbEndpoint TEXT NOT NULL UNIQUE,
                      PsbP256dh TEXT NOT NULL,
                      PsbAuth TEXT NOT NULL,
                      PsbUserAgent TEXT,
                      PsbDtCriacao TEXT NOT NULL DEFAULT (datetime('now'))");

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

        /// <summary>
        /// Converte strings vazias em coluna decimal nullable para NULL. Bug do EF Core
        /// SQLite: ao ler '' tenta decimal.Parse('') e lanca FormatException. Idempotente.
        /// </summary>
        private static void NormalizeEmptyDecimal(System.Data.Common.DbConnection connection, string tableName, string columnName)
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'";
            if (Convert.ToInt32(checkCmd.ExecuteScalar()) == 0) return;

            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = $"UPDATE {tableName} SET {columnName}=NULL WHERE TRIM({columnName})=''";
            updateCmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Mesmo problema do NormalizeEmptyDecimal, mas para colunas NOT NULL — seta '0'
        /// em vez de NULL para nao violar constraint.
        /// </summary>
        private static void NormalizeEmptyDecimalNotNull(System.Data.Common.DbConnection connection, string tableName, string columnName)
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'";
            if (Convert.ToInt32(checkCmd.ExecuteScalar()) == 0) return;

            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = $"UPDATE {tableName} SET {columnName}='0' WHERE TRIM({columnName})='' OR {columnName} IS NULL";
            updateCmd.ExecuteNonQuery();
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
            SeedSystemReferences(dbContext);

            // Admin padrao do tenant default — so seedado no startup do app, nao em
            // tenants criados via TenantsAdminController (que recebem admin proprio).
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

        /// <summary>
        /// Seed reutilizavel das tabelas de referencia (Acessos, Categorias) que
        /// todo tenant precisa ter populadas. Idempotente. Chamado no startup do
        /// app (para o tenant default) e tambem pelo TenantsAdminController quando
        /// um tenant novo eh criado.
        /// </summary>
        public static void SeedSystemReferences(ConnectVeiculosDbContext dbContext)
        {
            // Seed de niveis de acesso (insere apenas os que não existem)
            var acessosSeed = new[]
            {
                ("Administrador", "Acesso total ao sistema, gerencia usuarios, lojas e configuracoes"),
                ("Gerente", "Gerencia veiculos, vendas, relatorios e equipe da loja"),
                ("Vendedor", "Cadastra e edita veiculos, registra vendas e atende leads"),
                ("Visualizador", "Acesso somente leitura, consulta veiculos, vendas e relatorios")
            };
            foreach (var (nome, desc) in acessosSeed)
            {
                if (!dbContext.Acessos.Any(a => a.AcsNome == nome))
                {
                    dbContext.Acessos.Add(new Core.Entities.Acessos.Acesso(0, nome, desc, true));
                }
            }
            dbContext.SaveChanges();

            // Seed de categorias (insere apenas as que não existem)
            var categoriasSeed = new[]
            {
                ("Sedan", "Veículos sedan de 4 portas"),
                ("Hatch", "Veículos hatchback compactos"),
                ("SUV", "Utilitários esportivos"),
                ("Picape", "Veículos de caçamba"),
                ("Conversível", "Veículos com teto retrátil"),
                ("Minivan", "Veículos para família com maior espaço"),
                ("Coupé", "Veículos esportivos de 2 portas"),
                ("Utilitário", "Veículos utilitários e comerciais"),
                ("Elétrico", "Veículos 100% elétricos"),
                ("Híbrido", "Veículos com motor híbrido"),
            };
            foreach (var (nome, desc) in categoriasSeed)
            {
                if (!dbContext.Categorias.Any(c => c.CatNome == nome))
                {
                    dbContext.Categorias.Add(new Core.Entities.Categorias.Categoria(0, nome, desc, true));
                }
            }
            dbContext.SaveChanges();
        }
    }
}
