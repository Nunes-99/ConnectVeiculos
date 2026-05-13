using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.Interfaces.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;

namespace ConnectVeiculos.Application.UseCases.RecuperacaoSenha
{
    public class RedefinirSenhaUseCase : IRedefinirSenhaUseCase
    {
        private readonly IRecuperacaoSenhaOperations _recuperacaoOperations;
        private readonly IUsuarioOperations _usuarioOperations;

        public RedefinirSenhaUseCase(
            IRecuperacaoSenhaOperations recuperacaoOperations,
            IUsuarioOperations usuarioOperations)
        {
            _recuperacaoOperations = recuperacaoOperations;
            _usuarioOperations = usuarioOperations;
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

            // Marcar token como utilizado
            recuperacao.MarcarComoUtilizado();
            await _recuperacaoOperations.AtualizarAsync(recuperacao);
        }
    }
}
