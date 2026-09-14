using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.MercadoLivre
{
    /// <inheritdoc cref="IMercadoLivreSincronizacaoService"/>
    public sealed class MercadoLivreSincronizacaoService : IMercadoLivreSincronizacaoService
    {
        private readonly IMercadoLivreService _mercadoLivreService;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoPublicacaoRepository _publicacaoRepository;
        private readonly ILogger<MercadoLivreSincronizacaoService> _logger;

        public MercadoLivreSincronizacaoService(
            IMercadoLivreService mercadoLivreService,
            IVeiculoRepository veiculoRepository,
            IVeiculoPublicacaoRepository publicacaoRepository,
            ILogger<MercadoLivreSincronizacaoService> logger)
        {
            _mercadoLivreService = mercadoLivreService;
            _veiculoRepository = veiculoRepository;
            _publicacaoRepository = publicacaoRepository;
            _logger = logger;
        }

        public async Task<MercadoLivreSincronizacaoResultado> SincronizarDisponiveisAsync()
        {
            var resultado = new MercadoLivreSincronizacaoResultado();

            if (!await _mercadoLivreService.IsConnectedAsync())
            {
                _logger.LogInformation("Sincronizacao do Mercado Livre pulada: conta nao conectada.");
                return resultado;
            }

            var disponiveis = (await _veiculoRepository.GetAllAsync())
                .Where(v => v.VeiSts == "D")
                .ToList();

            resultado.TotalDisponiveis = disponiveis.Count;

            foreach (var veiculo in disponiveis)
            {
                try
                {
                    var existente = await _publicacaoRepository
                        .GetAtivaByVeiculoEPlataformaAsync(veiculo.VeiId, "MercadoLivre");

                    if (existente != null)
                    {
                        resultado.JaPublicados++;
                        continue;
                    }

                    var (externoId, url, aguardandoPagamento) =
                        await _mercadoLivreService.PublicarVeiculoAsync(veiculo.VeiId);

                    await _publicacaoRepository.CreateAsync(
                        new VeiculoPublicacao(veiculo.VeiId, "MercadoLivre", externoId, url, aguardandoPagamento));

                    resultado.NovosPublicados++;
                    if (aguardandoPagamento) resultado.AguardandoPagamento++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Erro ao publicar veiculo {VeiculoId} no Mercado Livre durante a sincronizacao", veiculo.VeiId);

                    resultado.Falhas.Add(new MercadoLivreSincronizacaoFalha
                    {
                        VeiculoId = veiculo.VeiId,
                        Descricao = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno} ({veiculo.VeiPlaca})",
                        Erro = ex.Message
                    });
                }
            }

            return resultado;
        }
    }
}
