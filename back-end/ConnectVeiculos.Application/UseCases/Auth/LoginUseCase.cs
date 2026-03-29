using ConnectVeiculos.Application.InputModels.Auth;
using ConnectVeiculos.Application.Interfaces.Auth;
using ConnectVeiculos.Application.ViewModels.Auth;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ConnectVeiculos.Application.UseCases.Auth
{
    public class LoginUseCase : ILoginUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IConfiguration _configuration;

        public LoginUseCase(IUsuarioRepository usuarioRepository, IConfiguration configuration)
        {
            _usuarioRepository = usuarioRepository;
            _configuration = configuration;
        }

        public async Task<LoginViewModel?> Execute(LoginInputModel input)
        {
            var usuario = await _usuarioRepository.GetByEmailAsync(input.Email);

            if (usuario == null)
                return null;

            // Verificar senha com BCrypt
            if (!BCrypt.Net.BCrypt.Verify(input.Senha, usuario.UsuSenha))
                return null;

            // Verificar se usuario esta ativo
            if (!usuario.UsuSts)
                return null;

            // Gerar token JWT com role
            var token = GenerateJwtToken(usuario.UsuId, usuario.UsuEmail, usuario.UsuNome, usuario.UsuFuncao);

            return new LoginViewModel
            {
                UsuId = usuario.UsuId,
                UsuNome = usuario.UsuNome,
                UsuEmail = usuario.UsuEmail,
                UsuFuncao = usuario.UsuFuncao ?? "",
                Token = token.Token,
                Expiration = token.Expiration
            };
        }

        private (string Token, DateTime Expiration) GenerateJwtToken(int userId, string email, string nome, string? funcao)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var expirationHours = int.Parse(jwtSettings["ExpirationInHours"] ?? "8");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, nome),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Adicionar role ao token se existir
            if (!string.IsNullOrEmpty(funcao))
            {
                claims.Add(new Claim(ClaimTypes.Role, funcao));
            }

            var expiration = DateTime.UtcNow.AddHours(expirationHours);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiration,
                signingCredentials: credentials
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
        }
    }
}
