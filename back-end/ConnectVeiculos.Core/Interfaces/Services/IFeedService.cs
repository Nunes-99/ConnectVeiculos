namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IFeedService
    {
        Task<string> GerarFeedFacebookAsync();
        Task<string> GerarFeedGoogleAsync();
    }
}
