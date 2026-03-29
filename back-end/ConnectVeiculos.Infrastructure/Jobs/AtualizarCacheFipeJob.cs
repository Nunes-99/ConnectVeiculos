using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.Fipe;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    public class AtualizarCacheFipeJob : IBackgroundJob
    {
        private readonly IFipeService _fipeService;
        private readonly ILogger<AtualizarCacheFipeJob> _logger;

        public string JobName => "AtualizarCacheFipe";
        public string CronExpression => "0 2 1 * *"; // Primeiro dia do mes as 2h

        public AtualizarCacheFipeJob(
            IFipeService fipeService,
            ILogger<AtualizarCacheFipeJob> logger)
        {
            _fipeService = fipeService;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Iniciando atualizacao do cache FIPE...");

            try
            {
                // Atualiza cache de marcas para cada tipo de veiculo
                var tipos = new[] { FipeTipoVeiculo.Carros, FipeTipoVeiculo.Motos, FipeTipoVeiculo.Caminhoes };

                foreach (var tipo in tipos)
                {
                    _logger.LogInformation("Atualizando marcas para tipo: {Tipo}", tipo);
                    await _fipeService.GetMarcasAsync(tipo);
                }

                _logger.LogInformation("Atualizacao do cache FIPE concluida.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar cache FIPE.");
                throw;
            }
        }
    }
}
