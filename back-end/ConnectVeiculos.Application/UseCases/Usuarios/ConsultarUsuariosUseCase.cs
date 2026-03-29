using ConnectVeiculos.Application.Interfaces.Usuarios;
using ConnectVeiculos.Application.ViewModels.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;

namespace ConnectVeiculos.Application.UseCases.Usuarios
{
    public class ConsultarUsuariosUseCase : IConsultarUsuariosUseCase
    {
        private readonly IUsuarioOperations _usuarioOperations;

        public ConsultarUsuariosUseCase(IUsuarioOperations usuarioOperations)
        {
            _usuarioOperations = usuarioOperations;
        }

        public async Task<List<UsuarioViewModel>> Execute(string pesquisa, string inicio, string intervalo)
        {
            var result = await _usuarioOperations.ConsultarVisualizacaoUsuarios(pesquisa, inicio, intervalo);

            if (result == null)
                return new List<UsuarioViewModel>();

            var usuarios = ((IEnumerable<dynamic>)result).Select(u => new UsuarioViewModel
            {
                UsuId = (int)(long)u.UsuId,
                UsuNome = u.UsuNome,
                UsuCPF = u.UsuCPF,
                UsuRG = u.UsuRG,
                UsuEmail = u.UsuEmail,
                UsuFuncao = u.UsuFuncao,
                UsuSts = Convert.ToBoolean(u.UsuSts)
            }).ToList();

            return usuarios;
        }
    }
}
