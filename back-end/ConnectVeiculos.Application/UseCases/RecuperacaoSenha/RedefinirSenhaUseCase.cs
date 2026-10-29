using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.Interfaces.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.RefreshTokens;

namespace ConnectVeiculos.Application.UseCases.RecuperacaoSenha
{
    public class RedefinirSenhaUseCase : IRedefinirSenhaUseCase
    {
        private readonly IRecuperacaoSenhaOperations _recuperacaoOperations;
        private readonly IUsuarioOperations _usuarioOperations;
        private readonly IRefreshTokenRepository _refreshTokens;

        public RedefinirSenhaUseCase(
            IRecuperacaoSenhaOperations recuperacaoOperations,
            IUsuarioOperations usuarioOperations,
            IRefreshTokenRepository refreshTokens = null)
        {
            _recuperacaoOperations = recuperacaoOperations;
            _usuarioOperations = usuarioOperations;
            _refreshTokens = refreshTokens;
        }

        public async Task ExecutarAsync(RedefinirSenhaInputModel input)
        {
            var recuperacao = await _recuperacaoOperations.ObterPorTokenAsync(input.Token);

            if (recuperacao == null)
            {
                throw new InputModelException("Token inválido ou expirado.");
            }

            if (!recuperacao.IsValido())
            {
                throw new InputModelException("Token inválido ou expirado.");
            }

            // Hash da nova senha com BCrypt
            var senhaHash = BCrypt.Net.BCrypt.HashPassword(input.NovaSenha);

            // Atualizar senha do usuario
            await _usuarioOperations.AtualizarSenhaAsync(recuperacao.RecUsuId, senhaHash);

            // Senha nova derruba as sessoes abertas em outros aparelhos: quem trocou
            // a senha por desconfiar de invasao esperava tirar o intruso, mas o
            // refresh token dele continuava valendo por 7 dias.
            if (_refreshTokens != null)
                await _refreshTokens.RevokeAllByUsuarioIdAsync(recuperacao.RecUsuId);

            // Marcar token como utilizado
            recuperacao.MarcarComoUtilizado();
            await _recuperacaoOperations.AtualizarAsync(recuperacao);
        }
    }
}
