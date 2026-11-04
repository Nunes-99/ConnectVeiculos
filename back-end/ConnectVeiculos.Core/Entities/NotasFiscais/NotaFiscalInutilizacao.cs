namespace ConnectVeiculos.Core.Entities.NotasFiscais
{
    /// <summary>
    /// Faixa de numeracao inutilizada na Sefaz (numeros pulados que nunca
    /// viraram nota autorizada). Depois de homologada, esses numeros nao podem
    /// mais ser usados nesta serie.
    /// </summary>
    public class NotaFiscalInutilizacao
    {
        public int NfiId { get; private set; }
        public string NfiAmbiente { get; private set; } = "homologacao";
        public int NfiSerie { get; private set; }
        public int NfiNumeroInicial { get; private set; }
        public int NfiNumeroFinal { get; private set; }
        public string NfiJustificativa { get; private set; } = string.Empty;
        public string? NfiProtocolo { get; private set; }
        public DateTime NfiData { get; private set; }
        public string? NfiXml { get; private set; }

        public NotaFiscalInutilizacao() { }

        public NotaFiscalInutilizacao(string ambiente, int serie, int inicial, int final, string justificativa,
            string? protocolo, DateTime data, string? xml)
        {
            NfiAmbiente = ambiente;
            NfiSerie = serie;
            NfiNumeroInicial = inicial;
            NfiNumeroFinal = final;
            NfiJustificativa = justificativa;
            NfiProtocolo = protocolo;
            NfiData = data;
            NfiXml = xml;
        }

        public bool Contem(int numero) => numero >= NfiNumeroInicial && numero <= NfiNumeroFinal;
    }
}
