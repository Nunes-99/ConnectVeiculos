namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// O que a loja personaliza nas publicacoes do Instagram e do Facebook:
    /// o modelo da legenda de cada rede e a arte de capa (a primeira imagem)
    /// do post e do story. Campo vazio = o padrao do sistema.
    /// </summary>
    public interface IRedesPersonalizacaoService
    {
        Task<RedesPersonalizacaoInfo> GetAsync();

        /// <summary>Grava os modelos de legenda. Texto vazio volta ao padrao.</summary>
        Task SalvarLegendasAsync(string? legendaInstagram, string? legendaFacebook);

        /// <summary>
        /// Grava a imagem enviada como capa do post (<see cref="TipoArte.Post"/>)
        /// ou do story (<see cref="TipoArte.Story"/>) e apaga a anterior.
        /// Lanca <see cref="ArgumentException"/> se nao for uma imagem valida.
        /// </summary>
        Task<string> SalvarCapaAsync(string tipo, Stream conteudo);

        /// <summary>Tira a capa da loja: volta a arte padrao.</summary>
        Task RemoverCapaAsync(string tipo);

        /// <summary>
        /// Modelo de legenda que a loja usa nesta rede, ou o padrao do sistema
        /// se ela nao escreveu um. E' o texto que o painel mostra para editar.
        /// </summary>
        Task<string> ModeloLegendaAsync(string rede);

        /// <summary>
        /// A legenda pronta para um carro. <paramref name="modelo"/> nulo usa o
        /// da loja; o da loja vazio usa o padrao. Sem <paramref name="veiculoId"/>
        /// (previa em Integracoes) usa o carro mais recente, ou um de exemplo.
        /// </summary>
        Task<string> MontarLegendaAsync(int? veiculoId, string rede, string? modelo = null);

        /// <summary>
        /// URL publica da primeira imagem do post do feed: a capa que a loja
        /// enviou ou, sem ela, a arte padrao (foto + preco + loja) em 4:5.
        /// Null quando nao deu para montar — o post sai so com as fotos.
        /// </summary>
        Task<string?> CapaDoPostAsync(int veiculoId, string baseUrl);

        /// <summary>
        /// Arte em JPEG para a previa do painel. Sem <paramref name="veiculoId"/>
        /// usa o carro disponivel mais recente com foto.
        /// </summary>
        Task<byte[]?> PreviaArteAsync(string tipo, int? layout, int? veiculoId);
    }

    public static class TipoArte
    {
        public const string Post = "post";
        public const string Story = "story";

        public static bool Valido(string? tipo) => tipo == Post || tipo == Story;
    }

    public class RedesPersonalizacaoInfo
    {
        /// <summary>Modelo da loja; vazio quando ela usa o padrao.</summary>
        public string LegendaInstagram { get; set; } = "";
        public string LegendaFacebook { get; set; } = "";
        public string LegendaPadraoInstagram { get; set; } = "";
        public string LegendaPadraoFacebook { get; set; } = "";

        /// <summary>Caminho em /uploads da capa enviada, ou null.</summary>
        public string? CapaPost { get; set; }
        public string? CapaStory { get; set; }

        public int TamanhoMaximoLegenda { get; set; }
        public List<VariavelLegenda> Variaveis { get; set; } = new();
        public List<LayoutStoryInfo> Layouts { get; set; } = new();
    }

    public record VariavelLegenda(string Nome, string Descricao, string Exemplo);

    /// <summary>Um dos modelos de montagem da arte do story.</summary>
    public record LayoutStoryInfo(int Id, string Nome, string Descricao, int Fotos);

    public class LegendasInput
    {
        public string? LegendaInstagram { get; set; }
        public string? LegendaFacebook { get; set; }
    }
}
