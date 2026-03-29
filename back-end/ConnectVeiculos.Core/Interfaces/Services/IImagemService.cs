namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IImagemService
    {
        Task<ImagemProcessada> ProcessarImagemAsync(Stream imagemOriginal, string nomeArquivo, ImagemOpcoes opcoes = null);
        Task<string> GerarThumbnailAsync(string caminhoOriginal, int largura = 300, int altura = 200);
        Task<string> GerarImagemMediaAsync(string caminhoOriginal, int largura = 800, int altura = 600);
    }

    public class ImagemOpcoes
    {
        public int QualidadeJpeg { get; set; } = 85;
        public bool ConverterParaWebP { get; set; } = true;
        public int LarguraMaxima { get; set; } = 1920;
        public int AlturaMaxima { get; set; } = 1080;
        public bool GerarThumbnail { get; set; } = true;
        public bool GerarImagemMedia { get; set; } = true;
    }

    public class ImagemProcessada
    {
        public string CaminhoOriginal { get; set; }
        public string CaminhoThumbnail { get; set; }
        public string CaminhoMedia { get; set; }
        public long TamanhoOriginal { get; set; }
        public long TamanhoProcessado { get; set; }
        public double PercentualReducao { get; set; }
    }
}
