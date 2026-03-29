using ConnectVeiculos.Application.InputModels.Usuarios;
using ConnectVeiculos.Application.Interfaces.Usuarios;
using ConnectVeiculos.Core.Entities.LojasUsuarios;
using ConnectVeiculos.Core.Entities.Permissoes;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;

namespace ConnectVeiculos.Application.UseCases.Usuarios
{
    public class AtualizarUsuarioUseCase : IAtualizarUsuarioUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly ILojaUsuarioRepository _lojaUsuarioRepository;
        private readonly IPermissaoRepository _permissaoRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AtualizarUsuarioUseCase(
            IUsuarioRepository usuarioRepository,
            ILojaUsuarioRepository lojaUsuarioRepository,
            IPermissaoRepository permissaoRepository,
            IUnitOfWork unitOfWork)
        {
            _usuarioRepository = usuarioRepository;
            _lojaUsuarioRepository = lojaUsuarioRepository;
            _permissaoRepository = permissaoRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Execute(UsuarioInputModel inputModel)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(inputModel.UsuId);

            if (usuario == null)
                throw new Exception("Usuario nao encontrado.");

            // Se a senha foi alterada, faz o hash
            var senhaHash = string.IsNullOrEmpty(inputModel.UsuSenha)
                ? usuario.UsuSenha
                : BCrypt.Net.BCrypt.HashPassword(inputModel.UsuSenha);

            usuario.SetProperties(
                inputModel.UsuId,
                inputModel.UsuNome,
                inputModel.UsuCPF,
                inputModel.UsuRG,
                inputModel.UsuEmail,
                senhaHash,
                inputModel.UsuFuncao,
                inputModel.UsuSts
            );

            _unitOfWork.BeginTransaction();

            try
            {
                await _usuarioRepository.UpdateAsync(usuario);

                // Atualizar associacao com Loja
                if (inputModel.R_LojId > 0)
                {
                    var lojaUsuarioExistente = await _lojaUsuarioRepository.GetByUsuarioIdAsync(inputModel.UsuId);

                    if (lojaUsuarioExistente != null)
                    {
                        lojaUsuarioExistente.SetProperties(lojaUsuarioExistente.LojUsuId, inputModel.UsuId, inputModel.R_LojId, lojaUsuarioExistente.UsuAcs);
                        await _lojaUsuarioRepository.UpdateAsync(lojaUsuarioExistente);
                    }
                    else
                    {
                        var lojaUsuario = new LojaUsuario(0, inputModel.UsuId, inputModel.R_LojId, "S");
                        await _lojaUsuarioRepository.CreateAsync(lojaUsuario);
                    }
                }

                // Atualizar associacao com Acesso
                if (inputModel.R_AcsId > 0)
                {
                    var permissoesExistentes = await _permissaoRepository.GetByUsuarioIdAsync(inputModel.UsuId);
                    var permissaoExistente = permissoesExistentes.FirstOrDefault();

                    if (permissaoExistente != null)
                    {
                        permissaoExistente.SetProperties(permissaoExistente.UsuAcsId, inputModel.UsuId, inputModel.R_AcsId, permissaoExistente.AcsTp);
                        await _permissaoRepository.UpdateAsync(permissaoExistente);
                    }
                    else
                    {
                        var permissao = new Permissao(0, inputModel.UsuId, inputModel.R_AcsId, "S");
                        await _permissaoRepository.CreateAsync(permissao);
                    }
                }

                _unitOfWork.Commit();
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
