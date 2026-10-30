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

        /// <summary>
        /// Pix de 30 dias do plano (sem debito automatico): devolve o QR Code.
        /// O plano muda quando o aviso do pagamento aprovado chegar.
        /// </summary>
        Task<PixGerado> GerarPixAsync(int tenantId, int planoId, string emailPagador, CancellationToken ct = default);

        /// <summary>Volta ao Free as lojas cujo periodo pago por Pix acabou.</summary>
        Task<int> EncerrarPeriodosPixVencidosAsync(CancellationToken ct = default);

        /// <summary>
        /// Cancela. Ate 7 dias do pagamento (direito de arrependimento) devolve
        /// o valor e volta ao Free na hora; depois, o cartao deixa de cobrar e o
        /// plano segue ate o fim do periodo pago. Devolve a mensagem para a loja.
        /// </summary>
        Task<string> CancelarAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// Trata um aviso do Mercado Pago. Os dados sao sempre relidos na API
        /// deles com o nosso token: o corpo do aviso so diz o que mudou.
        /// </summary>
        Task ProcessarAvisoAsync(string tipo, string id, CancellationToken ct = default);
    }

    public class PixGerado
    {
        /// <summary>Codigo "copia e cola".</summary>
        public string Codigo { get; set; } = "";
        /// <summary>PNG do QR Code em base64 (sem o prefixo data:).</summary>
        public string QrCodeBase64 { get; set; } = "";
        public decimal Valor { get; set; }
        public DateTime ExpiraEm { get; set; }
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
