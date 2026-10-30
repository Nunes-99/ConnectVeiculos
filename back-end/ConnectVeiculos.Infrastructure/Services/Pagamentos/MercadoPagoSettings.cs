namespace ConnectVeiculos.Infrastructure.Services.Pagamentos
{
    /// <summary>
    /// Conta Mercado Pago que recebe as mensalidades (a da plataforma, nao a de
    /// cada loja). Env: MercadoPagoSettings__AccessToken, __WebhookSecret,
    /// __PublicSiteUrl. Sem o token, a assinatura pelo cartao fica desligada.
    /// </summary>
    public class MercadoPagoSettings
    {
        public string AccessToken { get; set; } = string.Empty;

        // "Assinatura secreta" da aplicacao no painel do Mercado Pago (Webhooks):
        // confere que o aviso veio mesmo deles.
        public string WebhookSecret { get; set; } = string.Empty;

        // Para onde o Mercado Pago devolve o cliente depois do pagamento.
        public string PublicSiteUrl { get; set; } = "https://connectveiculos.dev.br";
    }
}
