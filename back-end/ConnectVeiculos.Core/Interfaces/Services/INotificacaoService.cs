namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Servico para enviar notificacoes em tempo real
    /// </summary>
    public interface INotificacaoService
    {
        Task EnviarParaUsuarioAsync(int usuarioId, string tipo, object dados);
        Task EnviarParaGrupoAsync(string grupo, string tipo, object dados);
        Task EnviarParaTodosAsync(string tipo, object dados);
    }
}
