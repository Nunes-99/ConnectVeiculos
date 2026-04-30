namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Push instantaneo para Facebook Catalog (alem do feed XML automatico).
    /// Usa a Catalog Batch API para atualizar produtos em segundos.
    /// </summary>
    public interface IFacebookCatalogService
    {
        bool IsConfigured();
        Task PublicarVeiculoAsync(int veiculoId);
        Task RemoverVeiculoAsync(int veiculoId);
    }

    /// <summary>
    /// Push instantaneo para Google Merchant Center (alem do feed XML automatico).
    /// Usa Content API for Shopping para atualizar produtos em segundos.
    /// </summary>
    public interface IGoogleMerchantService
    {
        bool IsConfigured();
        Task PublicarVeiculoAsync(int veiculoId);
        Task RemoverVeiculoAsync(int veiculoId);
    }
}
