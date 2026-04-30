namespace ConnectVeiculos.Core.Entities.Configuracoes
{
    public class ConfiguracaoSistema
    {
        public int CfgId { get; private set; }
        public string CfgChave { get; private set; }
        public string CfgValor { get; private set; }
        public DateTime CfgDtAtualizacao { get; private set; }

        public ConfiguracaoSistema() { }

        public ConfiguracaoSistema(string chave, string valor)
        {
            CfgChave = chave;
            CfgValor = valor;
            CfgDtAtualizacao = DateTime.UtcNow;
        }

        public void AtualizarValor(string valor)
        {
            CfgValor = valor;
            CfgDtAtualizacao = DateTime.UtcNow;
        }
    }
}
