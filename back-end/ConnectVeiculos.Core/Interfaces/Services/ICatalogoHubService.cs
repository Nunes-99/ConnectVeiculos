namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Servico para enviar atualizacoes do catalogo em tempo real
    /// </summary>
    public interface ICatalogoHubService
    {
        /// <summary>
        /// Notifica clients conectados ao hub. Grupos sao escopo por tenant —
        /// somente clients do mesmo tenant recebem a notificacao.
        /// </summary>
        /// <param name="tenantSlug">Slug do tenant atual (ex: "inova-motor"). Vem do ITenantContext.</param>
        Task NotificarAtualizacaoCatalogo(string tenantSlug, int lojaId, string tipoEvento, object dados);
    }
}
