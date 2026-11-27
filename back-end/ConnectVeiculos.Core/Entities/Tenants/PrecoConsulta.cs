namespace ConnectVeiculos.Core.Entities.Tenants
{
    /// <summary>
    /// Preco fixo de uma consulta (servico da ZapCar) para um plano, ou para
    /// todos os planos quando PlaId e' nulo. Sem linha, vale custo + acrescimo do
    /// plano. Definido pela tela de precos da plataforma.
    /// </summary>
    public class PrecoConsulta
    {
        public int PcsId { get; private set; }
        public string PcsServico { get; private set; } = "";
        public int? PlaId { get; private set; }
        public long PcsCentavos { get; private set; }
        public DateTime PcsAtualizadoEm { get; private set; }

        public PrecoConsulta() { }

        public PrecoConsulta(string servico, int? planoId, decimal preco)
        {
            PcsServico = servico;
            PlaId = planoId;
            Alterar(preco);
        }

        public decimal Preco => PcsCentavos / 100m;

        public void Alterar(decimal preco)
        {
            PcsCentavos = (long)Math.Round(preco * 100m, MidpointRounding.AwayFromZero);
            PcsAtualizadoEm = DateTime.UtcNow;
        }
    }
}
