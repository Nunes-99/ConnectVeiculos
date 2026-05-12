namespace ConnectVeiculos.Application.ViewModels.Auth
{
    public class LoginViewModel
    {
        public int UsuId { get; set; }
        public string UsuNome { get; set; } = string.Empty;
        public string UsuEmail { get; set; } = string.Empty;
        public string UsuFuncao { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshExpiration { get; set; }
        public string TenantSlug { get; set; } = string.Empty;
        public string TenantNome { get; set; } = string.Empty;
    }
}
