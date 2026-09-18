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
    /// Nasceu porque a aplicacao do ML nao recebia refresh_token: o token durava
    /// 6h e alguem precisava reconectar na mao. Sem aviso, a loja descobria pela
    /// ausencia — ficava dias sem anunciar nada e so estranhava depois.
    ///
    /// Desde 18/09/2026 o offline_access esta habilitado na aplicacao (caixa
    /// "Refresh Token" nos fluxos OAuth do DevCenter) e o refresh funciona, entao
    /// o token deixou de expirar sozinho e este worker normalmente nao tem o que
    /// avisar. Continua valendo: se o refresh_token for revogado ou rejeitado, a
    /// integracao volta a expirar e o aviso e o que evita a loja descobrir tarde.
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

            var enviados = await EnviarAvisosAsync(sp, minutosRestantes, await MontarLinkPainelAsync(sp));
            await config.SetValorAsync(ChaveUltimoAviso, marca);

            _logger.LogInformation(
                "Aviso de expiracao do Mercado Livre enviado no tenant {Slug} ({Minutos} min restantes, {Enviados} destinatario(s)).",
                slug, (int)minutosRestantes, enviados);
        }

        /// <summary>"1 hora", "45 minutos", "1 minuto" — evita o "minuto(s)" que
        /// aparecia no texto e nao le como frase.</summary>
        private static string FormatarPrazo(double minutos)
        {
            var m = (int)Math.Round(minutos);
            if (m >= 60)
            {
                var horas = m / 60;
                var resto = m % 60;
                var texto = horas == 1 ? "1 hora" : $"{horas} horas";
                if (resto > 0) texto += resto == 1 ? " e 1 minuto" : $" e {resto} minutos";
                return texto;
            }
            return m == 1 ? "1 minuto" : $"{m} minutos";
        }

        /// <summary>
        /// Link direto pra tela de Integracoes. Sai do catalogo da loja padrao,
        /// que e a unica URL publica que o tenant conhece. Sem ela o e-mail vai
        /// sem botao e so descreve o caminho.
        /// </summary>
        private static async Task<string> MontarLinkPainelAsync(IServiceProvider sp)
        {
            try
            {
                var loja = sp.GetRequiredService<Core.Interfaces.Database.Repositories.Lojas.ILojaRepository>();
                var padrao = (await loja.GetAllAsync())
                    .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.LojUrlCatalogo));

                if (padrao?.LojUrlCatalogo is not string url) return "";
                if (!Uri.TryCreate(url.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)) return "";

                return $"{uri.Scheme}://{uri.Authority}/integracoes";
            }
            catch { return ""; }
        }

        private static async Task<int> EnviarAvisosAsync(IServiceProvider sp, double minutosRestantes, string linkPainel)
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
            var expirou = minutosRestantes <= 0;

            var assunto = expirou
                ? "Conexão com o Mercado Livre expirou"
                : "Conexão com o Mercado Livre expira em breve";

            var titulo = expirou ? "Conexão expirada" : "Conexão expirando";
            var cor = expirou ? "#b91c1c" : "#b45309";
            var fundoAviso = expirou ? "#fee2e2" : "#fef3c7";

            var chamada = expirou
                ? "A conexão da sua loja com o <strong>Mercado Livre</strong> expirou."
                : $"A conexão da sua loja com o <strong>Mercado Livre</strong> expira em {FormatarPrazo(minutosRestantes)}.";

            var corpo = $@"<!DOCTYPE html><html><body style='margin:0;padding:0;background:#f3f4f6;font-family:Arial,Helvetica,sans-serif;line-height:1.6;color:#333'>
<div style='max-width:600px;margin:0 auto;padding:20px'>
  <div style='background:{cor};color:#fff;padding:20px;text-align:center;border-radius:8px 8px 0 0'>
    <h1 style='margin:0;font-size:20px'>{titulo}</h1>
  </div>
  <div style='padding:24px;background:#fff;border:1px solid #e5e7eb;border-top:0;border-radius:0 0 8px 8px'>
    <p style='margin:0 0 16px'>{chamada}</p>

    <div style='background:{fundoAviso};border-left:4px solid {cor};padding:14px 16px;border-radius:6px;margin:0 0 20px'>
      <strong style='display:block;margin-bottom:4px'>O que acontece enquanto ela estiver fora</strong>
      Os veículos cadastrados não são anunciados, e os anúncios que já estão no ar
      deixam de ser atualizados.
    </div>

    <p style='margin:0 0 8px'><strong>Como resolver</strong></p>
    <p style='margin:0 0 20px'>
      Acesse <strong>Integrações</strong> no painel e clique em
      <strong>Conectar Mercado Livre</strong>. Leva alguns segundos. Os veículos que
      ficaram de fora são publicados automaticamente logo depois — você não precisa
      fazer mais nada.
    </p>

    {(string.IsNullOrEmpty(linkPainel) ? "" : $@"<p style='margin:0 0 20px'>
      <a href='{linkPainel}' style='display:inline-block;background:#1a237e;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600'>Abrir Integrações</a>
    </p>")}

    <p style='margin:0;padding-top:16px;border-top:1px solid #e5e7eb;color:#6b7280;font-size:13px'>
      O Mercado Livre não fornece renovação automática para este aplicativo, por isso a
      reconexão precisa ser feita manualmente de tempos em tempos.
    </p>
  </div>
  <p style='text-align:center;color:#9ca3af;font-size:12px;margin:16px 0 0'>
    ConnectVeículos &middot; mensagem automática
  </p>
</div>
</body></html>";

            var enviados = 0;
            foreach (var destinatario in destinatarios)
            {
                if (await email.SendEmailAsync(destinatario, assunto, corpo)) enviados++;
            }

            return enviados;
        }
    }
}
