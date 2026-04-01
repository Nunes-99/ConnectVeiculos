using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.Interfaces.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Email;

namespace ConnectVeiculos.Application.UseCases.RecuperacaoSenha
{
    public class SolicitarRecuperacaoSenhaUseCase : ISolicitarRecuperacaoSenhaUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRecuperacaoSenhaOperations _recuperacaoOperations;
        private readonly IEmailService _emailService;

        public SolicitarRecuperacaoSenhaUseCase(
            IUsuarioRepository usuarioRepository,
            IRecuperacaoSenhaOperations recuperacaoOperations,
            IEmailService emailService)
        {
            _usuarioRepository = usuarioRepository;
            _recuperacaoOperations = recuperacaoOperations;
            _emailService = emailService;
        }

        public async Task<string?> ExecutarAsync(SolicitarRecuperacaoInputModel input)
        {
            var usuario = await _usuarioRepository.GetByEmailAsync(input.Email);

            // Por seguranca, nao informamos se o email existe ou nao
            if (usuario == null || !usuario.UsuSts)
            {
                return null;
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

            // Enviar email com o token de recuperacao
            await _emailService.SendRecuperacaoSenhaAsync(
                usuario.UsuEmail,
                usuario.UsuNome,
                token
            );

            return token;
        }
    }
}
