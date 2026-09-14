namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Publica no Mercado Livre todos os veiculos disponiveis que ainda nao tem
    /// anuncio ativo.
    ///
    /// Existe separado do controller porque tem dois chamadores: o botao de
    /// sincronizar da tela de Integracoes e a reconexao do OAuth. O segundo e o
    /// que importa: o token do ML dura 6h e o app nao recebe refresh_token,
    /// entao a conta vive caindo. Enquanto esta caida, todo veiculo cadastrado
    /// fica de fora do Mercado Livre — sem isto, o operador teria que lembrar de
    /// clicar em sincronizar toda vez que reconectasse.
    /// </summary>
    public interface IMercadoLivreSincronizacaoService
    {
        Task<MercadoLivreSincronizacaoResultado> SincronizarDisponiveisAsync();
    }

    public class MercadoLivreSincronizacaoResultado
    {
        public int TotalDisponiveis { get; set; }
        public int NovosPublicados { get; set; }
        public int JaPublicados { get; set; }

        /// <summary>
        /// Quantos dos recem-criados o ML deixou invisiveis aguardando a taxa.
        /// Sem esse numero a tela dizia "publicados" pra anuncio nenhum no ar.
        /// </summary>
        public int AguardandoPagamento { get; set; }

        public List<MercadoLivreSincronizacaoFalha> Falhas { get; set; } = new();
    }

    public class MercadoLivreSincronizacaoFalha
    {
        public int VeiculoId { get; set; }
        public string Descricao { get; set; } = "";
        public string Erro { get; set; } = "";
    }
}
