namespace ConnectVeiculos.Core.Entities.Publicacoes
{
    public class VeiculoPublicacao
    {
        public int PubId { get; private set; }
        public int R_VeiId { get; private set; }
        public string PubPlataforma { get; private set; }
        public string PubExternoId { get; private set; }
        public string PubStatus { get; private set; }
        public string PubUrl { get; private set; }
        public DateTime? PubDtPublicacao { get; private set; }
        public DateTime? PubDtRemocao { get; private set; }

        /// <summary>Anuncio no ar na plataforma.</summary>
        public const string StatusAtivo = "ATIVO";

        /// <summary>
        /// Anuncio criado na plataforma mas invisivel ate o vendedor pagar a taxa.
        /// O Mercado Livre responde HTTP 402 nesse caso: o item existe e tem id,
        /// so nao aparece pra quem busca. Registrar como ATIVO fazia o sistema
        /// afirmar que o veiculo estava anunciado quando ninguem conseguia ve-lo.
        /// </summary>
        public const string StatusAguardandoPagamento = "AGUARDANDO_PAGAMENTO";

        /// <summary>Anuncio encerrado na plataforma.</summary>
        public const string StatusRemovido = "REMOVIDO";

        public VeiculoPublicacao() { }

        public VeiculoPublicacao(int rVeiId, string plataforma, string externoId, string url,
            bool aguardandoPagamento = false)
        {
            R_VeiId = rVeiId;
            PubPlataforma = plataforma;
            PubExternoId = externoId;
            PubStatus = aguardandoPagamento ? StatusAguardandoPagamento : StatusAtivo;
            PubUrl = url;
            PubDtPublicacao = DateTime.UtcNow;
        }

        public void Remover()
        {
            PubStatus = StatusRemovido;
            PubDtRemocao = DateTime.UtcNow;
        }

        /// <summary>
        /// Move de "aguardando pagamento" para ativo — usado quando a plataforma
        /// avisa (webhook) que o anuncio entrou no ar.
        /// </summary>
        public void MarcarComoAtivo()
        {
            PubStatus = StatusAtivo;
            PubDtRemocao = null;
        }

        /// <summary>O anuncio existe na plataforma, esteja no ar ou aguardando taxa.</summary>
        public bool EstaPublicado =>
            PubStatus == StatusAtivo || PubStatus == StatusAguardandoPagamento;

        public void AtualizarExternoId(string externoId, string url)
        {
            PubExternoId = externoId;
            PubUrl = url;
        }
    }
}
