using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.MercadoLivre
{
    /// <summary>
    /// Avisa por e-mail quando a conexao com o Mercado Livre esta perto de cair.
    ///
    /// O app do ML nao recebe refresh_token, entao o token dura 6h e alguem
    /// precisa reconectar na mao. Sem aviso, a loja descobre pela ausencia: fica
    /// dias sem anunciar nada e so estranha depois. O e-mail chega antes, com o
    /// link da tela de Integracoes.
    ///
    /// Quando o ML liberar offline_access na aplicacao, o refresh passa a
    /// funcionar, o token deixa de expirar sozinho e este worker para de ter o
    /// que avisar — sem precisar ser removido.
    /// </summary>
    public sealed class MercadoLivreExpiracaoWorker : BackgroundService
    {
        // De quanto em quanto tempo varrer os tenants. O token dura 6h; 15min de
        // resolucao e suficiente e mantem o custo irrisorio.
        private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

        // Com quanto tempo de antecedencia avisar.
        private const int MinutosDeAntecedencia = 60;

        // Guarda no proprio tenant qual expiracao ja foi avisada, pra nao mandar
        // o mesmo e-mail a cada varredura. Reconectar gera uma expiracao nova e
        // o aviso volta a valer.
        private const string ChaveUltimoAviso = "ML_EXPIRACAO_AVISADA_EM";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantStore _tenantStore;
        private readonly ILogger<MercadoLivreExpiracaoWorker> _logger;

        public MercadoLivreExpiracaoWorker(
            IServiceScopeFactory scopeFactory,
            ITenantStore tenantStore,
            ILogger<MercadoLivreExpiracaoWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _tenantStore = tenantStore;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Nao corre junto com a subida da aplicacao: o banco de cada tenant
            // ainda pode estar aplicando schema.
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await VerificarTodosOsTenantsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha na varredura de expiracao do Mercado Livre.");
                }

                try { await Task.Delay(Intervalo, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task VerificarTodosOsTenantsAsync(CancellationToken ct)
        {
            var tenants = await _tenantStore.ListActiveAsync();

            foreach (var tenant in tenants)
            {
                if (ct.IsCancellationRequested) return;

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    scope.ServiceProvider
                        .GetRequiredService<ITenantContext>()
                        .Resolve(tenant.TenId, tenant.TenSlug, tenant.TenDatabaseFile);

                    await VerificarTenantAsync(scope.ServiceProvider, tenant.TenSlug);
                }
                catch (Exception ex)
                {
                    // Um tenant com problema nao pode impedir o aviso dos outros.
                    _logger.LogError(ex,
                        "Falha ao verificar a expiracao do Mercado Livre no tenant {Slug}.", tenant.TenSlug);
                }
            }
        }

        private async Task VerificarTenantAsync(IServiceProvider sp, string slug)
        {
            var ml = sp.GetRequiredService<IMercadoLivreService>();
            if (!await ml.IsConnectedAsync()) return;

            var expiraEm = await ml.ObterExpiracaoTokenAsync();
            if (expiraEm == null) return;

            var minutosRestantes = (expiraEm.Value - DateTime.UtcNow).TotalMinutes;
            if (minutosRestantes > MinutosDeAntecedencia) return;

            var config = sp.GetRequiredService<IConfiguracaoSistemaRepository>();
            var jaAvisado = await config.GetValorAsync(ChaveUltimoAviso);
            var marca = expiraEm.Value.ToString("O");

            if (string.Equals(jaAvisado, marca, StringComparison.Ordinal)) return;

            var enviados = await EnviarAvisosAsync(sp, minutosRestantes);
            await config.SetValorAsync(ChaveUltimoAviso, marca);

            _logger.LogInformation(
                "Aviso de expiracao do Mercado Livre enviado no tenant {Slug} ({Minutos} min restantes, {Enviados} destinatario(s)).",
                slug, (int)minutosRestantes, enviados);
        }

        private static async Task<int> EnviarAvisosAsync(IServiceProvider sp, double minutosRestantes)
        {
            var usuarios = await sp.GetRequiredService<IUsuarioRepository>().GetAllAsync();

            var destinatarios = usuarios
                .Where(u => u.UsuSts
                            && !string.IsNullOrWhiteSpace(u.UsuEmail)
                            && string.Equals(u.UsuFuncao, "Administrador", StringComparison.OrdinalIgnoreCase))
                .Select(u => u.UsuEmail)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (destinatarios.Count == 0) return 0;

            var email = sp.GetRequiredService<IEmailService>();
            var quando = minutosRestantes <= 0
                ? "ja expirou"
                : $"expira em cerca de {(int)minutosRestantes} minuto(s)";

            var assunto = minutosRestantes <= 0
                ? "ConnectVeiculos - conexao com o Mercado Livre expirou"
                : "ConnectVeiculos - conexao com o Mercado Livre esta expirando";

            var corpo = $@"
<p>A conexao da sua loja com o <strong>Mercado Livre</strong> {quando}.</p>
<p>Enquanto ela estiver fora, os veiculos cadastrados <strong>nao sao anunciados</strong> e os
anuncios existentes deixam de ser atualizados.</p>
<p>Para voltar ao normal, entre em <strong>Integracoes</strong> e clique em
<strong>Conectar Mercado Livre</strong>. Os veiculos que ficaram de fora sao publicados
automaticamente logo depois.</p>
<p style=""color:#666;font-size:13px"">O Mercado Livre nao fornece renovacao automatica para
este aplicativo, por isso a reconexao precisa ser feita manualmente.</p>";

            var enviados = 0;
            foreach (var destinatario in destinatarios)
            {
                if (await email.SendEmailAsync(destinatario, assunto, corpo)) enviados++;
            }

            return enviados;
        }
    }
}
