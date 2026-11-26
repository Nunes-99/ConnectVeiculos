namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>Creditos de consulta por placa de cada loja (saldo em reais).</summary>
    public interface ICreditosConsultaService
    {
        Task<decimal> SaldoAsync(int tenantId, CancellationToken ct = default);

        /// <summary>Credita um Pix aprovado. False se esse pagamento ja tinha sido creditado.</summary>
        Task<bool> CreditarCompraAsync(int tenantId, decimal valor, string pagamentoId, CancellationToken ct = default);

        /// <summary>Debita se houver saldo. False = saldo insuficiente (nada debitado).</summary>
        Task<bool> DebitarAsync(int tenantId, decimal valor, string referencia, string descricao, CancellationToken ct = default);

        /// <summary>Devolve o valor de um debito. False se ja tinha sido devolvido.</summary>
        Task<bool> EstornarAsync(int tenantId, decimal valor, string referencia, string descricao, CancellationToken ct = default);

        Task<IReadOnlyList<MovimentoCredito>> ExtratoAsync(int tenantId, int limite, CancellationToken ct = default);
    }

    public class MovimentoCredito
    {
        public string Tipo { get; set; } = "";
        public decimal Valor { get; set; }
        public string? Descricao { get; set; }
        public DateTime Data { get; set; }
    }
}
