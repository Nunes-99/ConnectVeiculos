using ConnectVeiculos.Application.UseCases.Estoque;
using ConnectVeiculos.Application.ViewModels.Estoque;

namespace ConnectVeiculos.Application.Interfaces.Estoque
{
    /// <summary>Alerta de carros parados no estoque e sugestao de preco.</summary>
    public interface IEstoqueParadoUseCase
    {
        /// <summary>Resumo e lista dos disponiveis em atencao/critico (opcionalmente de uma loja).</summary>
        Task<EstoqueParadoViewModel> ConsultarAsync(int? lojaId = null);

        /// <summary>Sugestao para um veiculo (qualquer situacao); nulo se nao existe.</summary>
        Task<EstoqueParadoItemViewModel?> SugestaoAsync(int veiculoId);

        Task<EstoqueParadoConfig> ConfigAsync();
        Task<EstoqueParadoConfig> SalvarConfigAsync(EstoqueParadoConfig config);
    }
}
