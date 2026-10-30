namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Assinatura mensal do plano no cartao (Mercado Pago). O cartao fica no
    /// Mercado Pago: o sistema so cria a assinatura, manda a loja para a pagina
    /// de pagamento deles e troca o plano quando o aviso (webhook) confirma.
    /// </summary>
    public interface IAssinaturaService
    {
        /// <summary>False sem MERCADOPAGO_ACCESS_TOKEN: a tela volta a oferecer o WhatsApp.</summary>
        bool Configurado { get; }

        /// <summary>
        /// Cria a assinatura pendente e devolve o link da pagina de pagamento.
        /// Lanca <see cref="AssinaturaException"/> com mensagem para o usuario.
        /// </summary>
        Task<string> IniciarAsync(int tenantId, int planoId, string emailPagador, CancellationToken ct = default);

        /// <summary>Cancela a assinatura ativa no Mercado Pago e volta a loja para o Free.</summary>
        Task CancelarAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// Trata um aviso do Mercado Pago. Os dados sao sempre relidos na API
        /// deles com o nosso token: o corpo do aviso so diz o que mudou.
        /// </summary>
        Task ProcessarAvisoAsync(string tipo, string id, CancellationToken ct = default);
    }

    public class AssinaturaException : Exception
    {
        public AssinaturaException(string mensagem) : base(mensagem) { }
    }

    public static class StatusAssinatura
    {
        // Os mesmos nomes que o Mercado Pago usa em /preapproval.
        public const string Pendente = "pending";
        public const string Ativa = "authorized";
        public const string Pausada = "paused";
        public const string Cancelada = "cancelled";
    }
}
