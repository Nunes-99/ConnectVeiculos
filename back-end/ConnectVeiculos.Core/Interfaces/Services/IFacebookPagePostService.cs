namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Post organico na timeline de uma Facebook Page. Diferente do Catalog (que alimenta
    /// Vehicle Ads pagos), aqui o post aparece como conteudo grauito no feed dos seguidores.
    /// </summary>
    public interface IFacebookPagePostService
    {
        Task<bool> IsConfiguredAsync();
        Task<FacebookPagePostConfigInfo> GetConfigAsync();
        Task SetAutoPostHabilitadoAsync(bool habilitado);
        Task<TestIntegracaoResult> TestarAsync();

        /// <summary>
        /// Publica veiculo na timeline da Page. Retorna resultado se publicou ou
        /// null se pulou. Hook deve persistir em VeiculoPublicacao.
        /// </summary>
        Task<PublicacaoResult?> PublicarVeiculoAsync(int veiculoId);

        /// <summary>Variante manual — ignora auto-post desabilitado.</summary>
        Task<PublicacaoResult?> PublicarManualAsync(int veiculoId);

        /// <summary>
        /// Reescreve a legenda de um post ja publicado com os dados atuais do
        /// veiculo. Serve pra tres coisas: carimbar VENDIDO / RESERVADO quando o
        /// carro sai, tirar o carimbo se a venda for estornada, e corrigir o preco
        /// depois de uma alteracao — o valor fica escrito na legenda, entao sem
        /// isso o post anuncia um preco que nao vale mais.
        ///
        /// O selo vem de <paramref name="statusVeiculo"/>: "D" nao carimba nada.
        ///
        /// So o Facebook permite isso. O Instagram nao tem endpoint pra editar
        /// legenda de midia publicada (a Graph API so deixa ligar/desligar
        /// comentarios), entao la o post fica como esta.
        /// </summary>
        Task<bool> AtualizarLegendaDoPostAsync(string postId, int veiculoId, string statusVeiculo);
    }

    public class FacebookPagePostConfigInfo
    {
        public bool PageConectada { get; set; }
        public string? PageId { get; set; }
        public string? PageNome { get; set; }
        public bool AutoPostHabilitado { get; set; }
    }
}
