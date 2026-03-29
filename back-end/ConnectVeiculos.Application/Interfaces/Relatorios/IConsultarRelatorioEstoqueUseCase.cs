using ConnectVeiculos.Application.ViewModels.Relatorios;

namespace ConnectVeiculos.Application.Interfaces.Relatorios
{
    public interface IConsultarRelatorioEstoqueUseCase
    {
        Task<RelatorioEstoqueViewModel> Execute(int? lojaId = null, int? categoriaId = null);
    }
}
