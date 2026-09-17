namespace ConnectVeiculos.Core.Entities.Tenants
{
    /// <summary>
    /// Registry global de numero do WhatsApp → tenant, no banco master.
    ///
    /// Existe porque o webhook do WhatsApp nao tem como dizer de quem e a
    /// mensagem pela URL. Ate 2026-09-17 a URL cadastrada na Meta carregava
    /// "?tenant=slug", o que funciona mas obriga **cada loja a ter o proprio
    /// aplicativo da Meta** — inviavel como produto: nenhum lojista cria app de
    /// desenvolvedor, gera token e configura webhook.
    ///
    /// Com um aplicativo unico atendendo todas as lojas, a URL passa a ser a
    /// mesma para todo mundo e quem identifica a loja e o `phone_number_id` que
    /// vem dentro do payload. Este mapa faz essa traducao.
    ///
    /// Mesmo papel do <see cref="UserEmailMap"/>, que resolve o tenant a partir
    /// do e-mail no login.
    /// </summary>
    public class WhatsAppNumeroMap
    {
        public int Id { get; private set; }

        /// <summary>
        /// O `phone_number_id` da Meta — identificador do numero, nao o numero
        /// em si. E o que chega no payload do webhook.
        /// </summary>
        public string PhoneNumberId { get; private set; } = string.Empty;

        public int TenantId { get; private set; }
        public string TenantSlug { get; private set; } = string.Empty;
        public DateTime AtualizadoEm { get; private set; } = DateTime.UtcNow;

        public WhatsAppNumeroMap() { }

        public WhatsAppNumeroMap(string phoneNumberId, int tenantId, string tenantSlug)
        {
            PhoneNumberId = phoneNumberId.Trim();
            TenantId = tenantId;
            TenantSlug = tenantSlug;
            AtualizadoEm = DateTime.UtcNow;
        }

        /// <summary>
        /// Um numero pode mudar de dono: a loja desconecta e outra conecta o
        /// mesmo numero. Reaponta em vez de criar uma segunda linha, que deixaria
        /// o roteamento ambiguo.
        /// </summary>
        public void Reapontar(int tenantId, string tenantSlug)
        {
            TenantId = tenantId;
            TenantSlug = tenantSlug;
            AtualizadoEm = DateTime.UtcNow;
        }
    }
}
