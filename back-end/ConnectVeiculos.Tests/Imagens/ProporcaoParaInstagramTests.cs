using ConnectVeiculos.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ConnectVeiculos.Tests.Imagens
{
    /// <summary>
    /// O Instagram recusava as fotos dos veiculos com
    /// "The aspect ratio is not supported" (code 36003): ele so aceita entre
    /// 4:5 (0.8) e 1.91:1, e num carrossel todos os itens precisam da mesma
    /// proporcao. Foto de carro chega em tudo quanto e formato — panoramica de
    /// celular, print quadrado, foto vertical — entao a API normaliza a
    /// proporcao no proprio endpoint que serve a imagem pra Meta.
    /// </summary>
    public class ProporcaoParaInstagramTests : IDisposable
    {
        private const double MinIg = 0.8;    // 4:5
        private const double MaxIg = 1.91;   // 1.91:1

        private readonly string _raiz;
        private readonly ImagensController _controller;

        public ProporcaoParaInstagramTests()
        {
            _raiz = Path.Combine(Path.GetTempPath(), "cv-img-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Path.Combine(_raiz, "uploads"));

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(_raiz);
            _controller = new ImagensController(env.Object);
        }

        [Theory]
        [InlineData(4000, 1200)]  // panoramica: 3.33:1, bem fora do limite
        [InlineData(1080, 1920)]  // vertical de celular: 0.56:1
        [InlineData(800, 800)]    // quadrada
        [InlineData(1600, 900)]   // 16:9, tambem fora (1.78 passa, mas o carrossel exige igualdade)
        [InlineData(300, 200)]    // menor que o alvo: precisa crescer, nao encolher
        public async Task DeveEntregarProporcaoAceitaPeloInstagram(int largura, int altura)
        {
            var caminho = CriarFoto(largura, altura);

            using var saida = await BaixarAsync(caminho, ratio: "4:5");

            var proporcao = (double)saida.Width / saida.Height;
            proporcao.Should().BeInRange(MinIg, MaxIg,
                "o Instagram rejeita o container fora dessa faixa");
            proporcao.Should().BeApproximately(0.8, 0.01, "foi pedido 4:5");
        }

        [Fact]
        public async Task TodasAsFotosDoCarrosselDevemSairComAMesmaProporcao()
        {
            // O erro original derrubava o post inteiro quando as fotos do mesmo
            // veiculo tinham formatos diferentes entre si.
            var caminhos = new[] { CriarFoto(4000, 1200), CriarFoto(1080, 1920), CriarFoto(800, 800) };

            var tamanhos = new List<Size>();
            foreach (var c in caminhos)
            {
                using var img = await BaixarAsync(c, ratio: "4:5");
                tamanhos.Add(img.Size);
            }

            tamanhos.Distinct().Should().HaveCount(1, "carrossel exige itens de mesma proporcao");
        }

        [Fact]
        public async Task DeveEncaixarSemCortarOVeiculo()
        {
            // Uma faixa vermelha no canto esquerdo representa a frente do carro.
            // Cortar pra preencher a moldura comeria essa faixa; o encaixe com
            // borda preserva a imagem inteira.
            var caminho = CriarFoto(4000, 1200, marcarBordaEsquerda: true);

            using var saida = await BaixarAsync(caminho, ratio: "4:5");

            var faixaPreservada = false;
            for (var y = 0; y < saida.Height && !faixaPreservada; y++)
            {
                var p = saida[1, y];
                if (p.R > 150 && p.G < 100 && p.B < 100) faixaPreservada = true;
            }

            faixaPreservada.Should().BeTrue("a imagem deve ser encaixada inteira, nao cortada");
        }

        [Fact]
        public async Task SemRatioDeveManterOComportamentoAntigo()
        {
            // O catalogo, o Mercado Livre e o Google seguem usando o endpoint sem
            // ratio e nao podem ganhar bordas brancas.
            var caminho = CriarFoto(2000, 1000);

            using var saida = await BaixarAsync(caminho, ratio: null);

            saida.Width.Should().Be(1440, "o lado maior e limitado ao alvo");
            saida.Height.Should().Be(720, "a proporcao original e preservada");
        }

        [Fact]
        public async Task DeveServirDoCacheNaSegundaChamada()
        {
            // Sem cache, cada download da Meta refazia o redimensionamento e o post
            // caia com "O download da midia demora muito" (subcode 2207003).
            var caminho = CriarFoto(4000, 1200);

            using (var _ = await BaixarAsync(caminho, ratio: "4:5")) { }

            var arquivosEmCache = Directory.Exists(Path.Combine(_raiz, "uploads", "_cache"))
                ? Directory.GetFiles(Path.Combine(_raiz, "uploads", "_cache"), "*.jpg")
                : Array.Empty<string>();
            arquivosEmCache.Should().ContainSingle();

            var resultado = await _controller.GetImageFile(caminho, max: 1440, format: "jpeg", ratio: "4:5");
            resultado.Should().BeOfType<PhysicalFileResult>("a segunda chamada vem do disco, sem reprocessar");
        }

        private async Task<Image<Rgba32>> BaixarAsync(string caminho, string? ratio)
        {
            var resultado = await _controller.GetImageFile(caminho, max: 1440, format: "jpeg", ratio: ratio);

            if (resultado is FileStreamResult fsr)
                return await Image.LoadAsync<Rgba32>(fsr.FileStream);

            if (resultado is PhysicalFileResult pfr)
                return await Image.LoadAsync<Rgba32>(pfr.FileName);

            throw new Xunit.Sdk.XunitException($"Resposta inesperada: {resultado.GetType().Name}");
        }

        private string CriarFoto(int largura, int altura, bool marcarBordaEsquerda = false)
        {
            var relativo = $"uploads/{Guid.NewGuid():N}.jpg";
            using var img = new Image<Rgba32>(largura, altura, new Rgba32(40, 90, 160));

            if (marcarBordaEsquerda)
            {
                for (var y = 0; y < altura; y++)
                    for (var x = 0; x < Math.Max(1, largura / 20); x++)
                        img[x, y] = new Rgba32(220, 30, 30);
            }

            img.Save(Path.Combine(_raiz, relativo.Replace('/', Path.DirectorySeparatorChar)),
                     new JpegEncoder { Quality = 95 });
            return relativo;
        }

        public void Dispose()
        {
            try { Directory.Delete(_raiz, recursive: true); } catch { }
        }
    }
}
