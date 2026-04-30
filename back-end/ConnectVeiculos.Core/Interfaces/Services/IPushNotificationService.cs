namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IPushNotificationService
    {
        string GetPublicKey();
        Task EnviarParaUsuarioAsync(int usuarioId, string titulo, string corpo, string? url = null);
        Task EnviarParaTodosAdminAsync(string titulo, string corpo, string? url = null);
    }
}
