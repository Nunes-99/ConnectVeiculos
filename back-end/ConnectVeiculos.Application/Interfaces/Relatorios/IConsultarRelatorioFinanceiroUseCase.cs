using ConnectVeiculos.Application.ViewModels.Relatorios;

namespace ConnectVeiculos.Application.Interfaces.Relatorios
{
    public interface IConsultarRelatorioFinanceiroUseCase
    {
        Task<RelatorioFinanceiroViewModel> Execute(DateTime? dataInicio = null, DateTime? dataFim = null, int? lojaId = null);
    }
}
