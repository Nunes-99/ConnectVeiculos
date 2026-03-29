using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ConnectVeiculos.Infrastructure.Services.Imagem
{
    public class ImagemService : IImagemService
    {
        private readonly ILogger<ImagemService> _logger;

        public ImagemService(ILogger<ImagemService> logger)
        {
            _logger = logger;
        }

        public async Task<ImagemProcessada> ProcessarImagemAsync(Stream imagemOriginal, string nomeArquivo, ImagemOpcoes opcoes = null)
        {
            opcoes ??= new ImagemOpcoes();

            try
            {
                var tamanhoOriginal = imagemOriginal.Length;
                imagemOriginal.Position = 0;

                using var image = await Image.LoadAsync(imagemOriginal);

                // Redimensionar se necessario
                if (image.Width > opcoes.LarguraMaxima || image.Height > opcoes.AlturaMaxima)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(opcoes.LarguraMaxima, opcoes.AlturaMaxima)
                    }));
                }

                // Determinar extensao
                var extensao = opcoes.ConverterParaWebP ? ".webp" : Path.GetExtension(nomeArquivo);
                var nomeBase = Path.GetFileNameWithoutExtension(nomeArquivo);
                var caminhoOriginal = $"{nomeBase}{extensao}";

                using var outputStream = new MemoryStream();

                if (opcoes.ConverterParaWebP)
                {
                    await image.SaveAsync(outputStream, new WebpEncoder { Quality = opcoes.QualidadeJpeg });
                }
                else
                {
                    await image.SaveAsync(outputStream, new JpegEncoder { Quality = opcoes.QualidadeJpeg });
                }

                var tamanhoProcessado = outputStream.Length;

                var resultado = new ImagemProcessada
                {
                    CaminhoOriginal = caminhoOriginal,
                    TamanhoOriginal = tamanhoOriginal,
                    TamanhoProcessado = tamanhoProcessado,
                    PercentualReducao = tamanhoOriginal > 0
                        ? Math.Round((1 - (double)tamanhoProcessado / tamanhoOriginal) * 100, 2)
                        : 0
                };

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar imagem: {NomeArquivo}", nomeArquivo);
                throw;
            }
        }

        public async Task<string> GerarThumbnailAsync(string caminhoOriginal, int largura = 300, int altura = 200)
        {
            try
            {
                if (!File.Exists(caminhoOriginal))
                    return null;

                using var image = await Image.LoadAsync(caminhoOriginal);

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Crop,
                    Size = new Size(largura, altura)
                }));

                var diretorio = Path.GetDirectoryName(caminhoOriginal);
                var nomeBase = Path.GetFileNameWithoutExtension(caminhoOriginal);
                var extensao = Path.GetExtension(caminhoOriginal);
                var caminhoThumbnail = Path.Combine(diretorio!, $"{nomeBase}_thumb{extensao}");

                await image.SaveAsync(caminhoThumbnail);

                return caminhoThumbnail;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar thumbnail: {Caminho}", caminhoOriginal);
                return null;
            }
        }

        public async Task<string> GerarImagemMediaAsync(string caminhoOriginal, int largura = 800, int altura = 600)
        {
            try
            {
                if (!File.Exists(caminhoOriginal))
                    return null;

                using var image = await Image.LoadAsync(caminhoOriginal);

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(largura, altura)
                }));

                var diretorio = Path.GetDirectoryName(caminhoOriginal);
                var nomeBase = Path.GetFileNameWithoutExtension(caminhoOriginal);
                var extensao = Path.GetExtension(caminhoOriginal);
                var caminhoMedia = Path.Combine(diretorio!, $"{nomeBase}_media{extensao}");

                await image.SaveAsync(caminhoMedia);

                return caminhoMedia;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar imagem media: {Caminho}", caminhoOriginal);
                return null;
            }
        }
    }
}
