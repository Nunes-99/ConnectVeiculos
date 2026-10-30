using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    /// <summary>
    /// Plano pago por Pix vale 30 dias por pagamento. Sem renovacao, a loja
    /// volta ao Free (os limites do Free passam a valer, nada e' apagado).
    /// </summary>
    public class EncerrarPlanosPixVencidosJob : IBackgroundJob
    {
        private readonly IAssinaturaService _assinaturas;
        private readonly ILogger<EncerrarPlanosPixVencidosJob> _logger;

        public string JobName => "EncerrarPlanosPixVencidos";
        public string CronExpression => "0 * * * *";

        public EncerrarPlanosPixVencidosJob(IAssinaturaService assinaturas, ILogger<EncerrarPlanosPixVencidosJob> logger)
        {
            _assinaturas = assinaturas;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            var n = await _assinaturas.EncerrarPeriodosPixVencidosAsync();
            if (n > 0) _logger.LogInformation("{Job}: {N} loja(s) voltaram ao plano gratuito", JobName, n);
        }
    }
}
