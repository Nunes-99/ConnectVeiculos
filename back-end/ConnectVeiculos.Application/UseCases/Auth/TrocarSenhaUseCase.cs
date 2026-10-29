using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.InputModels.Auth;
using ConnectVeiculos.Application.Interfaces.Auth;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.RefreshTokens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;

namespace ConnectVeiculos.Application.UseCases.Auth
{
    public class TrocarSenhaUseCase : ITrocarSenhaUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IUsuarioOperations _usuarioOperations;
        private readonly IRefreshTokenRepository _refreshTokens;

        public TrocarSenhaUseCase(
            IUsuarioRepository usuarioRepository,
            IUsuarioOperations usuarioOperations,
            IRefreshTokenRepository refreshTokens = null)
        {
            _usuarioRepository = usuarioRepository;
            _usuarioOperations = usuarioOperations;
            _refreshTokens = refreshTokens;
        }

        public async Task ExecutarAsync(int usuarioId, TrocarSenhaInputModel input)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);

            if (usuario == null)
            {
                throw new InputModelException("Usuário não encontrado.");
            }

            if (!BCrypt.Net.BCrypt.Verify(input.SenhaAtual, usuario.UsuSenha))
            {
                throw new InputModelException("Senha atual incorreta.");
            }

            if (BCrypt.Net.BCrypt.Verify(input.NovaSenha, usuario.UsuSenha))
            {
                throw new InputModelException("A nova senha deve ser diferente da senha atual.");
            }

            var novaSenhaHash = BCrypt.Net.BCrypt.HashPassword(input.NovaSenha);
            await _usuarioOperations.AtualizarSenhaAsync(usuarioId, novaSenhaHash);

            // Senha nova derruba as sessoes abertas em outros aparelhos: quem trocou
            // a senha por desconfiar de invasao esperava tirar o intruso, mas o
            // refresh token dele continuava valendo por 7 dias.
            if (_refreshTokens != null)
                await _refreshTokens.RevokeAllByUsuarioIdAsync(usuarioId);
        }
    }
}
