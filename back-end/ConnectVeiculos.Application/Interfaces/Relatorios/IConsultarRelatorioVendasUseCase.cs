using ConnectVeiculos.Application.ViewModels.Relatorios;

namespace ConnectVeiculos.Application.Interfaces.Relatorios
{
    public interface IConsultarRelatorioVendasUseCase
    {
        Task<RelatorioVendasViewModel> Execute(DateTime? dataInicio = null, DateTime? dataFim = null, int? lojaId = null);
    }
}
