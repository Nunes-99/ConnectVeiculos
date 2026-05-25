namespace ConnectVeiculos.Infrastructure.Services.MercadoLivre
{
    public class MercadoLivreSettings
    {
        public string AppId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string UserId { get; set; }
         // Instante UTC em que o access token expira (calculado a partir do
         // expires_in retornado pelo ML). Usado pelo refresh proativo.
         public DateTime? AccessTokenExpiraEm { get; set; }
    }
}
