using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConnectVeiculos.Infrastructure.Services.Stories
{
    /// <summary>O que vai escrito na arte. Campo nulo nao aparece.</summary>
    public sealed class StoryArteDados
    {
        public string Selo { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string? Detalhes { get; set; }
        public string? Preco { get; set; }
        public string? PrecoAnterior { get; set; }
        public string? Mensagem { get; set; }
        public string LojaNome { get; set; } = "";
        public string? Contato { get; set; }

        /// <summary>Cor da loja (#RRGGBB). Invalida ou vazia cai no vermelho padrao.</summary>
        public string? CorDestaque { get; set; }
    }

    /// <summary>
    /// Desenha a arte 1080x1920 do story: fundo com a propria foto desfocada,
    /// a foto inteira no meio e os dados do carro escritos em volta.
    ///
    /// Foto de carro e' quase sempre horizontal. Esticar para 9:16 cortaria o
    /// carro, e so encaixar deixaria duas faixas vazias enormes — o fundo
    /// desfocado preenche essas faixas com a cor da propria foto.
    /// </summary>
    public static class StoryArte
    {
        public const int Largura = 1080;
        public const int Altura = 1920;

        // O Instagram cobre o topo (barra de progresso, perfil) e o rodape
        // (campo de resposta) com a propria interface. Nada importante entra ai.
        private const int MargemSegura = 250;
        private const int LarguraTexto = 960;
        private const int FotoLarguraMax = 1000;
        private const int FotoAlturaMax = 640;
        private const int FotoAlturaMin = 380;

        private static readonly Color CorPadrao = Color.ParseHex("#E53935");
        private static readonly Color Branco = Color.White;
        private static readonly Color CinzaClaro = Color.ParseHex("#D5D9E0");

        private static readonly Lazy<(FontFamily Regular, FontFamily SemiBold, FontFamily ExtraBold)> Fontes =
            new(CarregarFontes);

        public static byte[] Gerar(Image foto, StoryArteDados dados)
        {
            var (regular, semiBold, extraBold) = Fontes.Value;
            var fonteSelo = extraBold.CreateFont(46);
            var fonteTitulo = extraBold.CreateFont(72);
            var fonteDetalhes = regular.CreateFont(40);
            var fonteAnterior = semiBold.CreateFont(44);
            var fontePreco = extraBold.CreateFont(112);
            var fonteMensagem = semiBold.CreateFont(52);
            var fonteLoja = semiBold.CreateFont(46);
            var fonteContato = regular.CreateFont(38);

            var destaque = ParseCor(dados.CorDestaque);

            // Mede tudo antes de desenhar: a altura da foto e' o que sobra, e o
            // bloco inteiro fica centralizado entre as margens seguras.
            var selo = dados.Selo.ToUpperInvariant();
            var hSelo = string.IsNullOrWhiteSpace(selo) ? 0f : Medir(selo, fonteSelo).Height + 44;
            var hTitulo = Medir(dados.Titulo, fonteTitulo).Height;
            var hDetalhes = Vazio(dados.Detalhes) ? 0f : Medir(dados.Detalhes!, fonteDetalhes).Height + 14;
            var hAnterior = Vazio(dados.PrecoAnterior) ? 0f : Medir(dados.PrecoAnterior!, fonteAnterior).Height + 16;
            var hPreco = Vazio(dados.Preco) ? 0f : Medir(dados.Preco!, fontePreco).Height;
            var hMensagem = Vazio(dados.Mensagem) ? 0f : Medir(dados.Mensagem!, fonteMensagem).Height;
            var hLoja = Medir(dados.LojaNome, fonteLoja).Height;
            var hContato = Vazio(dados.Contato) ? 0f : Medir(dados.Contato!, fonteContato).Height + 14;

            const float gapSelo = 44, gapFoto = 48, gapPreco = 44, gapLoja = 64;
            var textos = hSelo + (hSelo > 0 ? gapSelo : 0)
                       + gapFoto + hTitulo + hDetalhes
                       + (hAnterior + hPreco + hMensagem > 0 ? gapPreco : 0) + hAnterior + hPreco + hMensagem
                       + gapLoja + hLoja + hContato;

            var disponivel = Altura - 2 * MargemSegura;
            var fotoAltura = (int)Math.Clamp(disponivel - textos, FotoAlturaMin, FotoAlturaMax);

            using var fotoCentral = foto.CloneAs<Rgba32>();
            fotoCentral.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(FotoLarguraMax, fotoAltura)
            }));

            using var arte = MontarFundo(foto);

            var total = textos + fotoCentral.Height;
            var y = MargemSegura + Math.Max(0, (disponivel - total) / 2f);
            var centro = Largura / 2f;

            arte.Mutate(ctx =>
            {
                if (hSelo > 0)
                {
                    var tamanho = Medir(selo, fonteSelo);
                    var w = tamanho.Width + 80;
                    DesenharPilula(ctx, destaque, centro - w / 2, y, w, hSelo);
                    Escrever(ctx, selo, fonteSelo, Branco, centro, y + 22);
                    y += hSelo + gapSelo;
                }

                var fotoX = (Largura - fotoCentral.Width) / 2;
                ctx.Fill(Color.White.WithAlpha(0.9f),
                    new RectangularPolygon(fotoX - 6, y - 6, fotoCentral.Width + 12, fotoCentral.Height + 12));
                ctx.DrawImage(fotoCentral, new Point(fotoX, (int)y), 1f);
                y += fotoCentral.Height + gapFoto;

                Escrever(ctx, dados.Titulo, fonteTitulo, Branco, centro, y);
                y += hTitulo;

                if (hDetalhes > 0)
                {
                    y += 14;
                    Escrever(ctx, dados.Detalhes!, fonteDetalhes, CinzaClaro, centro, y);
                    y += hDetalhes - 14;
                }

                if (hAnterior + hPreco + hMensagem > 0) y += gapPreco;

                if (hAnterior > 0)
                {
                    var tamanho = Medir(dados.PrecoAnterior!, fonteAnterior);
                    Escrever(ctx, dados.PrecoAnterior!, fonteAnterior, CinzaClaro, centro, y);
                    // Risco no preco antigo: e' o que faz o "baixou" ser lido de relance.
                    var meio = y + tamanho.Height * 0.45f;
                    ctx.DrawLine(destaque, 5f,
                        new PointF(centro - tamanho.Width / 2 - 8, meio),
                        new PointF(centro + tamanho.Width / 2 + 8, meio));
                    y += hAnterior;
                }

                if (hPreco > 0)
                {
                    Escrever(ctx, dados.Preco!, fontePreco, Branco, centro, y);
                    y += hPreco;
                }

                if (hMensagem > 0)
                {
                    Escrever(ctx, dados.Mensagem!, fonteMensagem, Branco, centro, y);
                    y += hMensagem;
                }

                y += gapLoja;
                ctx.Fill(destaque, new RectangularPolygon(centro - 60, y - 28, 120, 6));
                Escrever(ctx, dados.LojaNome, fonteLoja, Branco, centro, y);
                y += hLoja;

                if (hContato > 0)
                    Escrever(ctx, dados.Contato!, fonteContato, CinzaClaro, centro, y + 14);
            });

            using var ms = new MemoryStream();
            arte.SaveAsJpeg(ms, new JpegEncoder { Quality = 88 });
            return ms.ToArray();
        }

        /// <summary>
        /// Foto em "cover" desfocada e escurecida. O desfoque roda numa versao
        /// pequena e depois amplia: em 1080x1920 ele levaria segundos na VM de 1
        /// OCPU, e o resultado visual e' o mesmo.
        /// </summary>
        private static Image<Rgba32> MontarFundo(Image foto)
        {
            var fundo = foto.CloneAs<Rgba32>();
            fundo.Mutate(x => x
                .Resize(new ResizeOptions { Mode = ResizeMode.Crop, Size = new Size(Largura / 4, Altura / 4) })
                .GaussianBlur(10)
                .Resize(Largura, Altura)
                .Fill(Color.Black.WithAlpha(0.62f)));
            return fundo;
        }

        private static void DesenharPilula(IImageProcessingContext ctx, Color cor, float x, float y, float w, float h)
        {
            var r = h / 2;
            ctx.Fill(cor, new RectangularPolygon(x + r, y, w - 2 * r, h));
            ctx.Fill(cor, new EllipsePolygon(x + r, y + r, r));
            ctx.Fill(cor, new EllipsePolygon(x + w - r, y + r, r));
        }

        private static void Escrever(IImageProcessingContext ctx, string texto, Font fonte, Color cor, float centroX, float topo)
        {
            ctx.DrawText(new RichTextOptions(fonte)
            {
                Origin = new PointF(centroX, topo),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                WrappingLength = LarguraTexto
            }, texto, cor);
        }

        private static FontRectangle Medir(string texto, Font fonte)
            => TextMeasurer.MeasureSize(texto, new TextOptions(fonte) { WrappingLength = LarguraTexto });

        private static bool Vazio(string? s) => string.IsNullOrWhiteSpace(s);

        private static Color ParseCor(string? hex)
        {
            if (!string.IsNullOrWhiteSpace(hex) && Color.TryParseHex(hex.Trim(), out var cor))
                return cor;
            return CorPadrao;
        }

        private static (FontFamily, FontFamily, FontFamily) CarregarFontes()
        {
            var assembly = typeof(StoryArte).Assembly;

            // Uma colecao por arquivo: os tres pesos se declaram da familia
            // "Poppins", e numa colecao so o CreateFont escolheria sempre o Regular.
            FontFamily Carregar(string arquivo)
            {
                var nome = assembly.GetManifestResourceNames().First(n => n.EndsWith(arquivo, StringComparison.Ordinal));
                using var stream = assembly.GetManifestResourceStream(nome)!;
                return new FontCollection().Add(stream);
            }

            return (Carregar("Poppins-Regular.ttf"), Carregar("Poppins-SemiBold.ttf"), Carregar("Poppins-ExtraBold.ttf"));
        }
    }
}
