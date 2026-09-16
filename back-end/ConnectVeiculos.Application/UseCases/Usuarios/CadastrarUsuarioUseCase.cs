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
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using System.Security.Cryptography;

namespace ConnectVeiculos.Application.UseCases.Usuarios
{
    public class CadastrarUsuarioUseCase : ICadastrarUsuarioUseCase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly ILojaUsuarioRepository _lojaUsuarioRepository;
        private readonly IPermissaoRepository _permissaoRepository;
        private readonly IUnitOfWork _unitOfWork;
         private readonly ILimiteService _limiteService;
        private readonly ITenantBackgroundRunner _backgroundRunner;

        public CadastrarUsuarioUseCase(
            IUsuarioRepository usuarioRepository,
            ILojaUsuarioRepository lojaUsuarioRepository,
            IPermissaoRepository permissaoRepository,
             IUnitOfWork unitOfWork,
             ILimiteService limiteService,
            ITenantBackgroundRunner backgroundRunner)
        {
            _usuarioRepository = usuarioRepository;
            _lojaUsuarioRepository = lojaUsuarioRepository;
            _permissaoRepository = permissaoRepository;
            _unitOfWork = unitOfWork;
             _limiteService = limiteService;
            _backgroundRunner = backgroundRunner;
        }

        /// <summary>
        /// Senha temporaria do primeiro acesso.
        ///
        /// Sem "I", "l", "1", "O" e "0": ela e lida de um e-mail e digitada a
        /// mao, e esses caracteres se confundem em boa parte das fontes.
        /// RandomNumberGenerator, e nao Random, porque isto e credencial.
        /// </summary>
        private const string AlfabetoSenha = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        private static string GerarSenhaTemporaria()
        {
            var chars = new char[12];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = AlfabetoSenha[RandomNumberGenerator.GetInt32(AlfabetoSenha.Length)];
            return new string(chars);
        }

        public async Task<int> Execute(UsuarioInputModel inputModel)
        {
             await _limiteService.GarantirPodeCriarUsuarioAsync();
            // Verificar se email já existe
            var existente = await _usuarioRepository.GetByEmailAsync(inputModel.UsuEmail);
            if (existente != null)
                throw new DomainException("Já existe um usuário cadastrado com este e-mail.");

            // A senha e gerada aqui, nao pedida ao administrador: assim ninguem
            // alem do proprio usuario conhece a senha com que ele vai entrar.
            // Ela vale so para o primeiro acesso — o UsuTrocarSenha abaixo
            // obriga a troca antes de liberar qualquer tela.
            var senhaTemporaria = GerarSenhaTemporaria();
            var senhaHash = BCrypt.Net.BCrypt.HashPassword(senhaTemporaria);

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
            usuario.ExigirTrocaDeSenha();

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

                // Depois do commit e fora da requisicao: o SMTP e lento e pode
                // estar fora do ar, e isso nao pode impedir o cadastro nem
                // segurar a tela do administrador. Se o e-mail falhar, o
                // administrador ainda pode reenviar pela tela de usuarios.
                var email = usuario.UsuEmail;
                var nome = usuario.UsuNome;
                _backgroundRunner.Enqueue<IEmailService>(
                    s => s.SendNovoUsuarioAsync(email, nome, senhaTemporaria),
                    $"enviar senha de primeiro acesso para {email}");

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
