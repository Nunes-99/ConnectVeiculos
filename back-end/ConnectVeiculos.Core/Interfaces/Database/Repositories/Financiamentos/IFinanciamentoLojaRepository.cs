using ConnectVeiculos.Core.Entities.Financiamentos;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Financiamentos
{
    public interface IFinanciamentoLojaRepository
    {
        Task<IReadOnlyList<BancoFinanciamentoLoja>> ListarBancosAsync();
        Task<BancoFinanciamentoLoja?> ObterBancoAsync(int id);
        Task SalvarBancoAsync(BancoFinanciamentoLoja banco);
        Task RemoverBancoAsync(BancoFinanciamentoLoja banco);

        Task<IReadOnlyList<FichaFinanciamento>> ListarFichasAsync(int? vendedorId);
        Task<FichaFinanciamento?> ObterFichaAsync(int id);
        Task SalvarFichaAsync(FichaFinanciamento ficha);
    }
}
