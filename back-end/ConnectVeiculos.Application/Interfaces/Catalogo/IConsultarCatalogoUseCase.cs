using ConnectVeiculos.Application.ViewModels.Catalogo;

namespace ConnectVeiculos.Application.Interfaces.Catalogo
{
    public interface IConsultarCatalogoUseCase
    {
        Task<CatalogoResultadoViewModel> Execute(string marca, int? anoMin, int? anoMax, decimal? precoMin, decimal? precoMax, int? lojaId = null);
    }
}
