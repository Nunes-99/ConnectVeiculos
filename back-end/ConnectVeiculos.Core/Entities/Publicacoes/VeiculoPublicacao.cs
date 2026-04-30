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

        public VeiculoPublicacao() { }

        public VeiculoPublicacao(int rVeiId, string plataforma, string externoId, string url)
        {
            R_VeiId = rVeiId;
            PubPlataforma = plataforma;
            PubExternoId = externoId;
            PubStatus = "ATIVO";
            PubUrl = url;
            PubDtPublicacao = DateTime.UtcNow;
        }

        public void Remover()
        {
            PubStatus = "REMOVIDO";
            PubDtRemocao = DateTime.UtcNow;
        }

        public void AtualizarExternoId(string externoId, string url)
        {
            PubExternoId = externoId;
            PubUrl = url;
        }
    }
}
