namespace ConnectVeiculos.Core.Entities.Despesas
{
    public class VeiculoDespesa
    {
        public int DesId { get; private set; }
        public int R_VeiId { get; private set; }
        public string DesTipo { get; private set; } // Manutencao, Documentacao, Estetica, Combustivel, Outros
        public string DesDescricao { get; private set; }
        public decimal DesValor { get; private set; }
        public DateTime DesDtDespesa { get; private set; }
        public DateTime DesDtCriacao { get; private set; }

        public VeiculoDespesa() { }

        public VeiculoDespesa(int desId, int rVeiId, string desTipo, string desDescricao, decimal desValor, DateTime desDtDespesa)
        {
            DesId = desId;
            R_VeiId = rVeiId;
            DesTipo = desTipo;
            DesDescricao = desDescricao;
            DesValor = desValor;
            DesDtDespesa = desDtDespesa;
            DesDtCriacao = DateTime.Now;
        }
    }
}
