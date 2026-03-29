namespace ConnectVeiculos.Core.Interfaces.Email
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body);
        Task<bool> SendVendaConfirmadaAsync(string to, string compradorNome, string veiculoDescricao, decimal valorVenda);
        Task<bool> SendVendaEstornadaAsync(string to, string compradorNome, string veiculoDescricao);
        Task<bool> SendNovoUsuarioAsync(string to, string usuarioNome, string senhaTemporaria);
    }
}
