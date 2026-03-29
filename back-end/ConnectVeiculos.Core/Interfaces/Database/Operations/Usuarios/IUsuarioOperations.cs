namespace ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios
{
    public interface IUsuarioOperations
    {
        Task<dynamic> ConsultarVisualizacaoUsuarios(string pesquisa, string inicio, string intervalo);
        Task<dynamic> ConsultarManutencaoUsuario(int id);
        Task<dynamic> ConsultarUsuarioPorEmail(string email);
        Task AtualizarSenhaAsync(int usuarioId, string senhaHash);
    }
}
