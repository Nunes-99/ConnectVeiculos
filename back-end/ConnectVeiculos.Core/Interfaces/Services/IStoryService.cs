namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Story (some em 24h) no Instagram e na Page do Facebook. A arte e' gerada
    /// no servidor em 9:16, com foto, preco e nome da loja escritos na imagem:
    /// story publicado pela API nao tem legenda nem figurinha de link, entao
    /// tudo o que precisa ser lido vai desenhado.
    ///
    /// Usa as mesmas permissoes dos posts (instagram_content_publish e
    /// pages_manage_posts) — nao acrescenta nada ao App Review da Meta.
    /// </summary>
    public interface IStoryService
    {
        Task<StoryConfigInfo> GetConfigAsync();
        Task SalvarConfigAsync(StoryConfigInput config);

        /// <summary>
        /// Publica o story de um evento (<see cref="EventoStory"/>) se a loja
        /// ligou o interruptor dele. Roda em background: nunca lanca.
        /// </summary>
        Task PublicarAutomaticoAsync(int veiculoId, string evento, decimal? precoAnterior = null);

        /// <summary>
        /// Botao "Publicar story" da lista de veiculos. Ignora os interruptores
        /// de evento, mas respeita onde a loja escolheu publicar.
        /// </summary>
        Task<StoryPublicacaoResultado> PublicarManualAsync(int veiculoId);
    }

    public static class EventoStory
    {
        public const string Novo = "NOVO";
        public const string PrecoBaixou = "PRECO";
        public const string Vendido = "VENDIDO";
        public const string Manual = "MANUAL";
    }

    public class StoryConfigInput
    {
        public bool NoInstagram { get; set; }
        public bool NoFacebook { get; set; }
        public bool AoChegar { get; set; }
        public bool AoBaixarPreco { get; set; }
        public bool AoVender { get; set; }
    }

    public class StoryConfigInfo : StoryConfigInput
    {
        public bool InstagramConectado { get; set; }
        public bool FacebookConectado { get; set; }
    }

    public class StoryPublicacaoResultado
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = "";
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
    }
}
