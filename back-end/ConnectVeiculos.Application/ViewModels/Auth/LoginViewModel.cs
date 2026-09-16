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

        /// <summary>
        /// Quando true, o front leva direto para a troca de senha e nao libera
        /// as demais telas. E o caso do primeiro acesso com a senha que o
        /// sistema gerou e mandou por e-mail.
        /// </summary>
        public bool TrocarSenhaObrigatoria { get; set; }
    }
}
