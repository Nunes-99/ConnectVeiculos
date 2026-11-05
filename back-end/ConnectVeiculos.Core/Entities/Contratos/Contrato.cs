namespace ConnectVeiculos.Core.Entities.Contratos
{
    /// <summary>
    /// Contrato gerado a partir de um modelo para um veiculo. O texto fica
    /// congelado na criacao (com o SHA-256 dele): o que cada um assina e
    /// exatamente esse texto. Tem duas partes: o cliente (comprador, vendedor
    /// ou consignante, conforme o tipo) e a loja.
    /// </summary>
    public class Contrato
    {
        public const string StatusAguardando = "AGUARDANDO";
        public const string StatusAssinado = "ASSINADO";
        public const string StatusCancelado = "CANCELADO";

        public int CtrId { get; private set; }
        public int R_VeiId { get; private set; }
        public int? R_MdcId { get; private set; }
        public int? R_UsuId { get; private set; }
        public string CtrTipo { get; private set; } = "OUTRO";
        public string CtrTitulo { get; private set; } = string.Empty;
        public string CtrConteudo { get; private set; } = string.Empty;
        public string CtrHash { get; private set; } = string.Empty;
        public string CtrStatus { get; private set; } = StatusAguardando;
        public DateTime CtrDtCriacao { get; private set; }
        public DateTime? CtrDtConclusao { get; private set; }
        public DateTime? CtrDtCancelamento { get; private set; }

        public List<ContratoSignatario> Signatarios { get; private set; } = new();

        public Contrato() { }

        public Contrato(int veiculoId, int? modeloId, int? usuarioId, string tipo, string titulo, string conteudo, string hash)
        {
            R_VeiId = veiculoId;
            R_MdcId = modeloId;
            R_UsuId = usuarioId;
            CtrTipo = tipo;
            CtrTitulo = titulo;
            CtrConteudo = conteudo;
            CtrHash = hash;
            CtrDtCriacao = DateTime.UtcNow;
        }

        public bool Aberto => CtrStatus == StatusAguardando;

        /// <summary>Fecha o contrato quando todos assinaram.</summary>
        public void Concluir()
        {
            if (Aberto && Signatarios.Count > 0 && Signatarios.All(s => s.Assinou))
            {
                CtrStatus = StatusAssinado;
                CtrDtConclusao = DateTime.UtcNow;
            }
        }

        public void Cancelar()
        {
            CtrStatus = StatusCancelado;
            CtrDtCancelamento = DateTime.UtcNow;
        }
    }
}
