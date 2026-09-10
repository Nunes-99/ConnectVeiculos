namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Entrega a notificacao do Mercado Livre ao tenant dono da conta ML.
    ///
    /// O webhook do ML e' anonimo e chega na URL crua do dominio, sem slug nem
    /// header de tenant — o middleware resolve pro tenant padrao. O resultado e'
    /// que a notificacao era processada no banco errado: procurava o anuncio,
    /// nao achava, e o status local nunca acompanhava o ML (venda, pausa,
    /// exclusao, expiracao). O unico vinculo confiavel e' o `user_id` do payload,
    /// que e' o seller id guardado por tenant em IntegracaoMercadoLivre.
    /// </summary>
    public interface IMercadoLivreWebhookRouter
    {
        /// <param name="topic">Topic do ML (items, orders, ...).</param>
        /// <param name="resource">Recurso, ex.: "/items/MLB123".</param>
        /// <param name="userId">Seller id dono da conta ML que originou o evento.</param>
        Task RotearAsync(string topic, string resource, string userId);
    }
}
