namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Servico para enviar atualizacoes do catalogo em tempo real
    /// </summary>
    public interface ICatalogoHubService
    {
        Task NotificarAtualizacaoCatalogo(int lojaId, string tipoEvento, object dados);
    }
}
