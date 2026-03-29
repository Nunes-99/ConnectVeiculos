namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IWebhookService
    {
        Task DispararAsync(string evento, object payload);
        Task DispararAsync<T>(string evento, T payload) where T : class;
    }
}
