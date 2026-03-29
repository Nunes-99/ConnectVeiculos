using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.Interfaces.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;

namespace ConnectVeiculos.Application.UseCases.RecuperacaoSenha
{
    public class SolicitarRecuperacaoSenhaUseCase : ISolicitarRecuperacaoSenhaUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRecuperacaoSenhaOperations _recuperacaoOperations;

        public SolicitarRecuperacaoSenhaUseCase(
            IUsuarioRepository usuarioRepository,
            IRecuperacaoSenhaOperations recuperacaoOperations)
        {
            _usuarioRepository = usuarioRepository;
            _recuperacaoOperations = recuperacaoOperations;
        }

        public async Task<string> ExecutarAsync(SolicitarRecuperacaoInputModel input)
        {
            var usuario = await _usuarioRepository.GetByEmailAsync(input.Email);

            if (usuario == null)
            {
                // Por seguranca, nao informamos se o email existe ou nao
                // Retornamos uma mensagem generica
                throw new InputModelException("Se o e-mail estiver cadastrado, voce recebera as instrucoes para recuperacao.");
            }

            if (!usuario.UsuSts)
            {
                throw new InputModelException("Usuario inativo. Entre em contato com o administrador.");
            }

            // Invalidar tokens anteriores
            await _recuperacaoOperations.InvalidarTokensAnterioresAsync(usuario.UsuId);

            // Gerar novo token
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var expiracao = DateTime.Now.AddHours(2); // Token valido por 2 horas

            var recuperacao = new Core.Entities.RecuperacaoSenha.RecuperacaoSenha(
                usuario.UsuId,
                token,
                expiracao
            );

            await _recuperacaoOperations.InserirAsync(recuperacao);

            // Em um sistema real, aqui enviariamos o email com o link de recuperacao
            // Por hora, retornamos o token para fins de teste/demonstracao
            return token;
        }
    }
}
