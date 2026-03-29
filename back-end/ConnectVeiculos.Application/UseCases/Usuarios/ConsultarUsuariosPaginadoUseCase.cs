using ConnectVeiculos.Application.Interfaces.Usuarios;
using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;

namespace ConnectVeiculos.Application.UseCases.Usuarios
{
    public class ConsultarUsuariosPaginadoUseCase : IConsultarUsuariosPaginadoUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly ILojaUsuarioRepository _lojaUsuarioRepository;
        private readonly IPermissaoRepository _permissaoRepository;

        public ConsultarUsuariosPaginadoUseCase(
            IUsuarioRepository usuarioRepository,
            ILojaUsuarioRepository lojaUsuarioRepository,
            IPermissaoRepository permissaoRepository)
        {
            _usuarioRepository = usuarioRepository;
            _lojaUsuarioRepository = lojaUsuarioRepository;
            _permissaoRepository = permissaoRepository;
        }

        public async Task<PagedResult<UsuarioViewModel>> Execute(int page, int pageSize, string? search = null)
        {
            var (items, total) = await _usuarioRepository.GetPagedAsync(page, pageSize, search);

            var usuarioIds = items.Select(u => u.UsuId).ToList();

            var lojasUsuarios = await _lojaUsuarioRepository.GetByUsuarioIdsAsync(usuarioIds);
            var permissoes = await _permissaoRepository.GetByUsuarioIdsAsync(usuarioIds);

            var viewModels = items.Select(u =>
            {
                var lojaUsuario = lojasUsuarios.FirstOrDefault(lu => lu.R_UsuId == u.UsuId);
                var permissao = permissoes.FirstOrDefault(p => p.R_UsuId == u.UsuId);

                return new UsuarioViewModel
                {
                    UsuId = u.UsuId,
                    R_LojId = lojaUsuario?.R_LojId ?? 0,
                    R_AcsId = permissao?.R_AcsId ?? 0,
                    LojaNome = lojaUsuario?.Loja?.LojNome ?? "",
                    AcessoNome = permissao?.Acesso?.AcsNome ?? "",
                    UsuNome = u.UsuNome,
                    UsuCPF = u.UsuCPF ?? "",
                    UsuRG = u.UsuRG ?? "",
                    UsuEmail = u.UsuEmail,
                    UsuFuncao = u.UsuFuncao ?? "",
                    UsuSts = u.UsuSts
                };
            }).ToList();

            return new PagedResult<UsuarioViewModel>(viewModels, total, page, pageSize);
        }
    }
}
