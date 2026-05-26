using ConnectVeiculos.Application.InputModels.Usuarios;
using ConnectVeiculos.Application.Interfaces.Usuarios;
using ConnectVeiculos.Core.Entities.LojasUsuarios;
using ConnectVeiculos.Core.Entities.Permissoes;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Application.UseCases.Usuarios
{
    public class CadastrarUsuarioUseCase : ICadastrarUsuarioUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly ILojaUsuarioRepository _lojaUsuarioRepository;
        private readonly IPermissaoRepository _permissaoRepository;
        private readonly IUnitOfWork _unitOfWork;
         private readonly ILimiteService _limiteService;

        public CadastrarUsuarioUseCase(
            IUsuarioRepository usuarioRepository,
            ILojaUsuarioRepository lojaUsuarioRepository,
            IPermissaoRepository permissaoRepository,
             IUnitOfWork unitOfWork,
             ILimiteService limiteService)
        {
            _usuarioRepository = usuarioRepository;
            _lojaUsuarioRepository = lojaUsuarioRepository;
            _permissaoRepository = permissaoRepository;
            _unitOfWork = unitOfWork;
             _limiteService = limiteService;
        }

        public async Task<int> Execute(UsuarioInputModel inputModel)
        {
             await _limiteService.GarantirPodeCriarUsuarioAsync();
            // Verificar se email já existe
            var existente = await _usuarioRepository.GetByEmailAsync(inputModel.UsuEmail);
            if (existente != null)
                throw new DomainException("Já existe um usuário cadastrado com este e-mail.");

            // Hash da senha com BCrypt
            var senhaHash = BCrypt.Net.BCrypt.HashPassword(inputModel.UsuSenha);

            var usuario = new Usuario(
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
                var id = await _usuarioRepository.CreateAsync(usuario);

                // Criar associacoes com Lojas
                var lojasParaAssociar = inputModel.LojasIds?.Where(l => l > 0).ToList();
                if (lojasParaAssociar != null && lojasParaAssociar.Any())
                {
                    foreach (var lojaId in lojasParaAssociar)
                    {
                        var lojaUsuario = new LojaUsuario(0, id, lojaId, "S");
                        await _lojaUsuarioRepository.CreateAsync(lojaUsuario);
                    }
                }
                else if (inputModel.R_LojId > 0)
                {
                    var lojaUsuario = new LojaUsuario(0, id, inputModel.R_LojId, "S");
                    await _lojaUsuarioRepository.CreateAsync(lojaUsuario);
                }

                // Criar associacao com Acesso
                if (inputModel.R_AcsId > 0)
                {
                    var permissao = new Permissao(0, id, inputModel.R_AcsId, "S");
                    await _permissaoRepository.CreateAsync(permissao);
                }

                _unitOfWork.Commit();
                return id;
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
