namespace ConnectVeiculos.Core.Entities.Tenants
{
    /// <summary>
    /// Movimento dos creditos de consulta da loja (no master, para o aviso do
    /// Pix creditar sem abrir o banco da loja). Saldo = soma dos centavos.
    /// A referencia e' unica: o mesmo Pix ou o mesmo estorno nao conta duas vezes.
    /// </summary>
    public class CreditoConsulta
    {
        public const string Compra = "COMPRA";
        public const string Consumo = "CONSUMO";
        public const string Estorno = "ESTORNO";
        public const string Ajuste = "AJUSTE";

        public int CrcId { get; private set; }
        public int TenId { get; private set; }
        /// <summary>Positivo entra, negativo sai.</summary>
        public long CrcCentavos { get; private set; }
        public string CrcTipo { get; private set; } = "";
        public string? CrcReferencia { get; private set; }
        public string? CrcDescricao { get; private set; }
        public DateTime CrcCriadoEm { get; private set; }

        public CreditoConsulta() { }
    }
}
