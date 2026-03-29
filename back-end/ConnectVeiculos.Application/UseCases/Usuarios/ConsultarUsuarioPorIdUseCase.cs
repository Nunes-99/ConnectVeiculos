using ConnectVeiculos.Application.Interfaces.Usuarios;
using ConnectVeiculos.Application.ViewModels.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes;

namespace ConnectVeiculos.Application.UseCases.Usuarios
{
    public class ConsultarUsuarioPorIdUseCase : IConsultarUsuarioPorIdUseCase
    {
        private readonly IUsuarioOperations _usuarioOperations;
        private readonly ILojaUsuarioRepository _lojaUsuarioRepository;
        private readonly IPermissaoRepository _permissaoRepository;

        public ConsultarUsuarioPorIdUseCase(
            IUsuarioOperations usuarioOperations,
            ILojaUsuarioRepository lojaUsuarioRepository,
            IPermissaoRepository permissaoRepository)
        {
            _usuarioOperations = usuarioOperations;
            _lojaUsuarioRepository = lojaUsuarioRepository;
            _permissaoRepository = permissaoRepository;
        }

        public async Task<UsuarioViewModel> Execute(int id)
        {
            var result = await _usuarioOperations.ConsultarManutencaoUsuario(id);

            if (result == null)
                return null;

            var lojaUsuario = await _lojaUsuarioRepository.GetByUsuarioIdAsync(id);
            var permissoes = await _permissaoRepository.GetByUsuarioIdAsync(id);
            var permissao = permissoes.FirstOrDefault();

            return new UsuarioViewModel
            {
                UsuId = (int)(long)result.UsuId,
                R_LojId = lojaUsuario?.R_LojId ?? 0,
                R_AcsId = permissao?.R_AcsId ?? 0,
                LojaNome = lojaUsuario?.Loja?.LojNome ?? "",
                AcessoNome = permissao?.Acesso?.AcsNome ?? "",
                UsuNome = result.UsuNome,
                UsuCPF = result.UsuCPF,
                UsuRG = result.UsuRG,
                UsuEmail = result.UsuEmail,
                UsuFuncao = result.UsuFuncao,
                UsuSts = Convert.ToBoolean(result.UsuSts)
            };
        }
    }
}
