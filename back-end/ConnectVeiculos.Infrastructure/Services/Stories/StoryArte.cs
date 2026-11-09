using ConnectVeiculos.Core.Interfaces.Services;
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
    /// Desenha as artes das redes: a do story (1080x1920) em um dos 10 layouts,
    /// a arte padrao do post do feed (1080x1350, 4:5) e o encaixe da capa que a
    /// loja enviou.
    ///
    /// Foto de carro e' quase sempre horizontal. Esticar para 9:16 cortaria o
    /// carro, e so encaixar deixaria duas faixas vazias enormes — o fundo
    /// desfocado preenche essas faixas com a cor da propria foto.
    /// </summary>
    public static class StoryArte
    {
        public const int Largura = 1080;
        public const int Altura = 1920;

        public const int LarguraFeed = 1080;
        public const int AlturaFeed = 1350;

        public const int LayoutPadrao = 1;

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
        private static readonly Color Escuro = Color.ParseHex("#1B1F24");
        private static readonly Color CinzaEscuro = Color.ParseHex("#5F6B7A");

        private static readonly Lazy<(FontFamily Regular, FontFamily SemiBold, FontFamily ExtraBold)> Familias =
            new(CarregarFontes);

        /// <summary>
        /// Os 10 modelos que a loja escolhe. <see cref="LayoutStoryInfo.Fotos"/>
        /// e' quantas fotos do carro o layout monta na arte.
        /// </summary>
        public static readonly IReadOnlyList<LayoutStoryInfo> Layouts = new[]
        {
            new LayoutStoryInfo(1, "Clássico", "Foto com moldura no centro, fundo desfocado e o preço embaixo.", 1),
            new LayoutStoryInfo(2, "Tela cheia", "A foto ocupa a tela toda, com os dados sobre uma sombra no rodapé.", 1),
            new LayoutStoryInfo(3, "Faixa da loja", "Foto em cima e uma faixa grande na cor da loja com os dados.", 1),
            new LayoutStoryInfo(4, "Dupla", "Duas fotos empilhadas e os dados embaixo.", 2),
            new LayoutStoryInfo(5, "Trio", "Uma foto grande e duas menores lado a lado.", 3),
            new LayoutStoryInfo(6, "Mosaico", "Quatro fotos em grade 2x2.", 4),
            new LayoutStoryInfo(7, "Cartão", "Um cartão branco com a foto e os dados, sobre o fundo desfocado.", 1),
            new LayoutStoryInfo(8, "Preço em destaque", "Foto de fundo e o preço num círculo grande na cor da loja.", 1),
            new LayoutStoryInfo(9, "Minimalista", "Fundo escuro liso, foto inteira e texto alinhado à esquerda.", 1),
            new LayoutStoryInfo(10, "Tira de fotos", "Três fotos em faixas, uma embaixo da outra.", 3)
        };

        public static bool LayoutValido(int layout) => layout >= 1 && layout <= Layouts.Count;

        /// <summary>
        /// Layout que da para montar com as fotos que o carro tem: o Mosaico de
        /// um carro com 2 fotos vira Dupla, em vez de repetir a mesma foto.
        /// </summary>
        public static int LayoutEfetivo(int layout, int fotosDisponiveis)
        {
            if (!LayoutValido(layout)) layout = LayoutPadrao;
            var precisa = Layouts[layout - 1].Fotos;
            if (fotosDisponiveis >= precisa) return layout;
            return fotosDisponiveis switch
            {
                >= 3 => 5,
                2 => 4,
                _ => LayoutPadrao
            };
        }

        public static int FotosDoLayout(int layout) => LayoutValido(layout) ? Layouts[layout - 1].Fotos : 1;

        /// <summary>Arte completa do story no layout classico.</summary>
        public static byte[] Gerar(Image foto, StoryArteDados dados)
            => ParaJpeg(Classico(foto, dados, Largura, Altura, MargemSegura, 1f, FotoAlturaMin, FotoAlturaMax));

        /// <summary>
        /// Arte do story no layout escolhido. Recebe as fotos na ordem do
        /// cadastro; usa so as que o layout pede (ver <see cref="LayoutEfetivo"/>).
        /// </summary>
        public static byte[] GerarLayout(int layout, IReadOnlyList<Image> fotos, StoryArteDados dados)
        {
            if (fotos.Count == 0) throw new ArgumentException("Sem foto para montar a arte.", nameof(fotos));
            var efetivo = LayoutEfetivo(layout, fotos.Count);
            using var arte = efetivo switch
            {
                2 => TelaCheia(fotos[0], dados),
                3 => FaixaDaLoja(fotos[0], dados),
                4 => Grade(fotos, dados, 4),
                5 => Grade(fotos, dados, 5),
                6 => Grade(fotos, dados, 6),
                7 => Cartao(fotos[0], dados),
                8 => PrecoEmDestaque(fotos[0], dados),
                9 => Minimalista(fotos[0], dados),
                10 => Grade(fotos, dados, 10),
                _ => Classico(fotos[0], dados, Largura, Altura, MargemSegura, 1f, FotoAlturaMin, FotoAlturaMax)
            };
            return ParaJpeg(arte);
        }

        /// <summary>
        /// Arte padrao do post do feed, em 4:5 — o formato que o carrossel do
        /// Instagram usa para todas as fotos. Mesma linguagem do story classico.
        /// </summary>
        public static byte[] GerarFeed(Image foto, StoryArteDados dados)
            => ParaJpeg(Classico(foto, dados, LarguraFeed, AlturaFeed, 70, 0.82f, 300, 560));

        /// <summary>
        /// Encaixa a capa enviada pela loja no tamanho da publicacao. Se ela ja
        /// tem a proporcao certa, so redimensiona; senao vai inteira sobre o
        /// fundo desfocado, sem cortar o que a loja escreveu nela.
        /// </summary>
        public static byte[] GerarCapa(Image capa, int largura, int altura)
        {
            var alvo = (double)largura / altura;
            var atual = (double)capa.Width / capa.Height;
            if (Math.Abs(atual - alvo) / alvo < 0.03)
            {
                using var exata = Cobrir(capa, largura, altura);
                return ParaJpeg(exata);
            }

            using var arte = MontarFundo(capa, largura, altura);
            using var encaixe = capa.CloneAs<Rgba32>();
            encaixe.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(largura, altura) }));
            arte.Mutate(ctx => ctx.DrawImage(encaixe,
                new Point((largura - encaixe.Width) / 2, (altura - encaixe.Height) / 2), 1f));
            return ParaJpeg(arte);
        }

        /// <summary>Reduz uma arte pronta para a previa do painel.</summary>
        public static byte[] Miniatura(byte[] jpeg, int largura)
        {
            using var img = Image.Load(jpeg);
            img.Mutate(x => x.Resize(largura, 0));
            using var ms = new MemoryStream();
            img.SaveAsJpeg(ms, new JpegEncoder { Quality = 82 });
            return ms.ToArray();
        }

        /// <summary>
        /// Story de continuacao ("tour" do carro): so a foto, grande, com uma
        /// faixa curta de modelo e preco. Os dados completos ja sairam na
        /// primeira arte da sequencia; repetir tudo aqui so tamparia a foto.
        /// Sem contador "2 DE 3": a loja preferiu a foto limpa.
        /// </summary>
        public static byte[] GerarFoto(Image foto, StoryArteDados dados)
        {
            var (_, semiBold, extraBold) = Familias.Value;
            var fonteTitulo = semiBold.CreateFont(54);
            var fontePreco = extraBold.CreateFont(76);

            var hTitulo = Medir(dados.Titulo, fonteTitulo).Height;
            var hPreco = Vazio(dados.Preco) ? 0f : Medir(dados.Preco!, fontePreco).Height + 6;
            const float gapFoto = 44;

            var disponivel = Altura - 2 * MargemSegura;
            var textos = gapFoto + hTitulo + hPreco;
            var fotoAltura = (int)Math.Max(FotoAlturaMin, disponivel - textos);

            using var fotoCentral = foto.CloneAs<Rgba32>();
            fotoCentral.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(Largura, fotoAltura)
            }));

            using var arte = MontarFundo(foto, Largura, Altura);

            var y = MargemSegura + Math.Max(0, (disponivel - textos - fotoCentral.Height) / 2f);
            var centro = Largura / 2f;

            arte.Mutate(ctx =>
            {
                // Sem moldura: a foto vai de borda a borda quando e' horizontal.
                ctx.DrawImage(fotoCentral, new Point((Largura - fotoCentral.Width) / 2, (int)y), 1f);
                y += fotoCentral.Height + gapFoto;

                Escrever(ctx, dados.Titulo, fonteTitulo, Branco, centro, y);
                y += hTitulo;

                if (hPreco > 0)
                    Escrever(ctx, dados.Preco!, fontePreco, Branco, centro, y + 6);
            });

            return ParaJpeg(arte);
        }

        // ------------------------------------------------------------------
        // Layout 1 — Classico (tambem a arte padrao do feed)
        // ------------------------------------------------------------------

        private static Image<Rgba32> Classico(Image foto, StoryArteDados dados,
            int largura, int altura, int margem, float escala, int fotoMin, int fotoMax)
        {
            var f = FontesArte.Criar(escala);
            var destaque = ParseCor(dados.CorDestaque);
            float E(float v) => v * escala;

            // Mede tudo antes de desenhar: a altura da foto e' o que sobra, e o
            // bloco inteiro fica centralizado entre as margens seguras.
            var selo = dados.Selo.ToUpperInvariant();
            var hSelo = string.IsNullOrWhiteSpace(selo) ? 0f : Medir(selo, f.Selo).Height + E(44);
            var hTitulo = Medir(dados.Titulo, f.Titulo).Height;
            var hDetalhes = Vazio(dados.Detalhes) ? 0f : Medir(dados.Detalhes!, f.Detalhes).Height + E(14);
            var hAnterior = Vazio(dados.PrecoAnterior) ? 0f : Medir(dados.PrecoAnterior!, f.Anterior).Height + E(16);
            var hPreco = Vazio(dados.Preco) ? 0f : Medir(dados.Preco!, f.Preco).Height;
            var hMensagem = Vazio(dados.Mensagem) ? 0f : Medir(dados.Mensagem!, f.Mensagem).Height;
            var hLoja = Vazio(dados.LojaNome) ? 0f : Medir(dados.LojaNome, f.Loja).Height;
            var hContato = Vazio(dados.Contato) ? 0f : Medir(dados.Contato!, f.Contato).Height + E(14);

            float gapSelo = E(44), gapFoto = E(48), gapPreco = E(44), gapLoja = E(64);
            var textos = hSelo + (hSelo > 0 ? gapSelo : 0)
                       + gapFoto + hTitulo + hDetalhes
                       + (hAnterior + hPreco + hMensagem > 0 ? gapPreco : 0) + hAnterior + hPreco + hMensagem
                       + gapLoja + hLoja + hContato;

            var disponivel = altura - 2 * margem;
            var fotoAltura = (int)Math.Clamp(disponivel - textos, fotoMin, fotoMax);

            using var fotoCentral = foto.CloneAs<Rgba32>();
            fotoCentral.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(Math.Min(FotoLarguraMax, largura - 80), fotoAltura)
            }));

            var arte = MontarFundo(foto, largura, altura);

            var total = textos + fotoCentral.Height;
            var y = margem + Math.Max(0, (disponivel - total) / 2f);
            var centro = largura / 2f;

            arte.Mutate(ctx =>
            {
                if (hSelo > 0)
                {
                    var tamanho = Medir(selo, f.Selo);
                    var w = tamanho.Width + E(80);
                    DesenharPilula(ctx, destaque, centro - w / 2, y, w, hSelo);
                    Escrever(ctx, selo, f.Selo, Branco, centro, y + E(22));
                    y += hSelo + gapSelo;
                }

                var fotoX = (largura - fotoCentral.Width) / 2;
                ctx.Fill(Color.White.WithAlpha(0.9f),
                    new RectangularPolygon(fotoX - 6, y - 6, fotoCentral.Width + 12, fotoCentral.Height + 12));
                ctx.DrawImage(fotoCentral, new Point(fotoX, (int)y), 1f);
                y += fotoCentral.Height + gapFoto;

                Escrever(ctx, dados.Titulo, f.Titulo, Branco, centro, y);
                y += hTitulo;

                if (hDetalhes > 0)
                {
                    y += E(14);
                    Escrever(ctx, dados.Detalhes!, f.Detalhes, CinzaClaro, centro, y);
                    y += hDetalhes - E(14);
                }

                if (hAnterior + hPreco + hMensagem > 0) y += gapPreco;

                if (hAnterior > 0)
                {
                    PrecoRiscado(ctx, dados.PrecoAnterior!, f.Anterior, CinzaClaro, destaque, centro, y, false);
                    y += hAnterior;
                }

                if (hPreco > 0)
                {
                    Escrever(ctx, dados.Preco!, f.Preco, Branco, centro, y);
                    y += hPreco;
                }

                if (hMensagem > 0)
                {
                    Escrever(ctx, dados.Mensagem!, f.Mensagem, Branco, centro, y);
                    y += hMensagem;
                }

                if (hLoja > 0)
                {
                    y += gapLoja;
                    ctx.Fill(destaque, new RectangularPolygon(centro - E(60), y - E(28), E(120), 6));
                    Escrever(ctx, dados.LojaNome, f.Loja, Branco, centro, y);
                    y += hLoja;
                }

                if (hContato > 0)
                    Escrever(ctx, dados.Contato!, f.Contato, CinzaClaro, centro, y + E(14));
            });

            return arte;
        }

        // ------------------------------------------------------------------
        // Layouts 2 a 10
        // ------------------------------------------------------------------

        /// <summary>2 — a foto cobre a tela; uma sombra no rodape segura o texto.</summary>
        private static Image<Rgba32> TelaCheia(Image foto, StoryArteDados dados)
        {
            var f = FontesArte.Criar(1f);
            var destaque = ParseCor(dados.CorDestaque);
            var bloco = Bloco.Medir(dados, f, LarguraTexto);

            var arte = Cobrir(foto, Largura, Altura);
            var yTexto = Altura - MargemSegura - bloco.Altura;
            arte.Mutate(ctx =>
            {
                // Sombra no topo para a barra do Instagram continuar legivel.
                ctx.Fill(Degrade(0, 360, Color.Black.WithAlpha(0.45f), Color.Transparent),
                    new RectangularPolygon(0, 0, Largura, 360));
                var inicio = Math.Max(0, yTexto - 380);
                ctx.Fill(Degrade(inicio, Altura, Color.Transparent, Color.Black.WithAlpha(0.92f)),
                    new RectangularPolygon(0, inicio, Largura, Altura - inicio));
                bloco.Desenhar(ctx, dados, f, Largura / 2f, yTexto, Cores.SobreEscuro(destaque));
            });
            return arte;
        }

        /// <summary>3 — foto em cima, faixa na cor da loja embaixo.</summary>
        private static Image<Rgba32> FaixaDaLoja(Image foto, StoryArteDados dados)
        {
            var f = FontesArte.Criar(1f);
            var destaque = ParseCor(dados.CorDestaque);
            var bloco = Bloco.Medir(dados, f, LarguraTexto);

            var yFaixa = (int)Math.Clamp(Altura - MargemSegura - bloco.Altura - 90, 700, 1240);
            var arte = new Image<Rgba32>(Largura, Altura, destaque.ToPixel<Rgba32>());
            using var topo = Cobrir(foto, Largura, yFaixa);

            // Texto escuro quando a cor da loja e' clara (amarelo, branco...).
            var clara = Luminancia(destaque) > 0.6;
            var texto = clara ? Escuro : Branco;
            var secundario = clara ? CinzaEscuro : Branco.WithAlpha(0.85f);
            var cores = new Cores(texto, secundario, Pilula: texto, TextoPilula: destaque, Barra: texto, Risco: texto);

            arte.Mutate(ctx =>
            {
                ctx.DrawImage(topo, new Point(0, 0), 1f);
                // Canto inclinado entre a foto e a faixa: tira o ar de "foto colada".
                ctx.Fill(destaque, new Polygon(new LinearLineSegment(
                    new PointF(0, yFaixa), new PointF(Largura, yFaixa - 70),
                    new PointF(Largura, yFaixa + 1), new PointF(0, yFaixa + 1))));
                var yTexto = yFaixa + Math.Max(40, (Altura - MargemSegura - yFaixa - bloco.Altura) / 2f);
                bloco.Desenhar(ctx, dados, f, Largura / 2f, yTexto, cores);
            });
            return arte;
        }

        /// <summary>
        /// 4 Dupla, 5 Trio, 6 Mosaico e 10 Tira: as fotos ocupam a parte de cima
        /// e o texto fica sobre o fundo desfocado embaixo.
        /// </summary>
        private static Image<Rgba32> Grade(IReadOnlyList<Image> fotos, StoryArteDados dados, int layout)
        {
            var f = FontesArte.Criar(0.9f);
            var destaque = ParseCor(dados.CorDestaque);
            var bloco = Bloco.Medir(dados, f, LarguraTexto);

            var yTexto = Altura - MargemSegura - bloco.Altura;
            var alturaFotos = (int)Math.Max(820, yTexto - 50);
            const int gap = 8;

            var arte = MontarFundo(fotos[0], Largura, Altura);

            // Celulas (x, y, largura, altura) de cada foto.
            var meiaL = (Largura - gap) / 2;
            var celulas = layout switch
            {
                4 => new[]
                {
                    (0, 0, Largura, (alturaFotos - gap) / 2),
                    (0, (alturaFotos + gap) / 2, Largura, (alturaFotos - gap) / 2)
                },
                5 => new[]
                {
                    (0, 0, Largura, (int)(alturaFotos * 0.6)),
                    (0, (int)(alturaFotos * 0.6) + gap, meiaL, alturaFotos - (int)(alturaFotos * 0.6) - gap),
                    (meiaL + gap, (int)(alturaFotos * 0.6) + gap, meiaL, alturaFotos - (int)(alturaFotos * 0.6) - gap)
                },
                6 => new[]
                {
                    (0, 0, meiaL, (alturaFotos - gap) / 2),
                    (meiaL + gap, 0, meiaL, (alturaFotos - gap) / 2),
                    (0, (alturaFotos + gap) / 2, meiaL, (alturaFotos - gap) / 2),
                    (meiaL + gap, (alturaFotos + gap) / 2, meiaL, (alturaFotos - gap) / 2)
                },
                _ => new[]
                {
                    (0, 0, Largura, (alturaFotos - 2 * gap) / 3),
                    (0, (alturaFotos - 2 * gap) / 3 + gap, Largura, (alturaFotos - 2 * gap) / 3),
                    (0, 2 * ((alturaFotos - 2 * gap) / 3 + gap), Largura, (alturaFotos - 2 * gap) / 3)
                }
            };

            arte.Mutate(ctx =>
            {
                for (var i = 0; i < celulas.Length; i++)
                {
                    var (x, y, w, h) = celulas[i];
                    using var celula = Cobrir(fotos[i], w, h);
                    ctx.DrawImage(celula, new Point(x, y), 1f);
                }
                // Linha na cor da loja separando as fotos do texto.
                ctx.Fill(destaque, new RectangularPolygon(0, alturaFotos, Largura, 10));
                var y0 = alturaFotos + 10 + Math.Max(30, (Altura - MargemSegura - alturaFotos - 10 - bloco.Altura) / 2f);
                bloco.Desenhar(ctx, dados, f, Largura / 2f, y0, Cores.SobreEscuro(destaque));
            });
            return arte;
        }

        /// <summary>7 — cartao branco com a foto e os dados.</summary>
        private static Image<Rgba32> Cartao(Image foto, StoryArteDados dados)
        {
            var f = FontesArte.Criar(0.9f);
            var destaque = ParseCor(dados.CorDestaque);
            const int margemX = 60, padding = 28;
            var larguraCartao = Largura - 2 * margemX;
            var larguraInterna = larguraCartao - 2 * padding;
            var bloco = Bloco.Medir(dados, f, larguraInterna);

            var disponivel = Altura - 2 * MargemSegura;
            var alturaFoto = (int)Math.Clamp(disponivel - bloco.Altura - 3 * padding - 30, 380, 820);
            var alturaCartao = alturaFoto + bloco.Altura + 3 * padding + 30;
            var yCartao = MargemSegura + Math.Max(0, (disponivel - alturaCartao) / 2f);

            var arte = MontarFundo(foto, Largura, Altura);
            using var imagem = Cobrir(foto, larguraInterna, alturaFoto);
            var cores = new Cores(Escuro, CinzaEscuro, destaque, Branco, destaque, destaque);

            arte.Mutate(ctx =>
            {
                RetanguloArredondado(ctx, Color.Black.WithAlpha(0.25f), margemX + 6, yCartao + 10, larguraCartao, alturaCartao, 36);
                RetanguloArredondado(ctx, Branco, margemX, yCartao, larguraCartao, alturaCartao, 36);
                ctx.DrawImage(imagem, new Point(margemX + padding, (int)yCartao + padding), 1f);
                bloco.Desenhar(ctx, dados, f, Largura / 2f, yCartao + padding + alturaFoto + padding + 30, cores);
            });
            return arte;
        }

        /// <summary>8 — foto de fundo e o preco num circulo grande.</summary>
        private static Image<Rgba32> PrecoEmDestaque(Image foto, StoryArteDados dados)
        {
            var f = FontesArte.Criar(1f);
            var destaque = ParseCor(dados.CorDestaque);
            var arte = Cobrir(foto, Largura, Altura);
            var centro = Largura / 2f;

            // Sem preco (carro vendido) o circulo leva o selo.
            var noCirculo = !Vazio(dados.Preco) ? dados.Preco! : dados.Selo.ToUpperInvariant();
            var seloNoTopo = !Vazio(dados.Preco) && !Vazio(dados.Selo);
            const float raio = 250;
            var fonteCirculo = f.Preco;
            for (var tamanho = fonteCirculo.Size; Medir(noCirculo, fonteCirculo).Width > raio * 1.7f && tamanho > 40; tamanho -= 6)
                fonteCirculo = new Font(fonteCirculo.Family, tamanho);

            var clara = Luminancia(destaque) > 0.6;
            var textoCirculo = clara ? Escuro : Branco;

            arte.Mutate(ctx =>
            {
                ctx.Fill(Color.Black.WithAlpha(0.35f), new RectangularPolygon(0, 0, Largura, Altura));
                ctx.Fill(Degrade(0, 760, Color.Black.WithAlpha(0.7f), Color.Transparent), new RectangularPolygon(0, 0, Largura, 760));
                ctx.Fill(Degrade(Altura - 700, Altura, Color.Transparent, Color.Black.WithAlpha(0.85f)),
                    new RectangularPolygon(0, Altura - 700, Largura, 700));

                var y = (float)MargemSegura;
                if (seloNoTopo)
                {
                    var selo = dados.Selo.ToUpperInvariant();
                    var tam = Medir(selo, f.Selo);
                    var h = tam.Height + 44;
                    DesenharPilula(ctx, destaque, centro - (tam.Width + 80) / 2, y, tam.Width + 80, h);
                    Escrever(ctx, selo, f.Selo, clara ? Escuro : Branco, centro, y + 22);
                    y += h + 36;
                }
                Escrever(ctx, dados.Titulo, f.Titulo, Branco, centro, y);
                y += Medir(dados.Titulo, f.Titulo).Height;
                if (!Vazio(dados.Detalhes))
                {
                    Escrever(ctx, dados.Detalhes!, f.Detalhes, CinzaClaro, centro, y + 14);
                }

                var cy = Altura * 0.56f;
                ctx.Fill(Color.Black.WithAlpha(0.3f), new EllipsePolygon(centro + 8, cy + 12, raio));
                ctx.Fill(destaque, new EllipsePolygon(centro, cy, raio));
                ctx.Draw(Branco, 8f, new EllipsePolygon(centro, cy, raio - 22));
                var hCirculo = Medir(noCirculo, fonteCirculo).Height;
                if (!Vazio(dados.PrecoAnterior))
                {
                    PrecoRiscado(ctx, dados.PrecoAnterior!, f.Anterior, textoCirculo, textoCirculo, centro, cy - hCirculo / 2 - 70, false);
                }
                Escrever(ctx, noCirculo, fonteCirculo, textoCirculo, centro, cy - hCirculo / 2 - 8);

                var yRodape = Altura - MargemSegura;
                var hContato = Vazio(dados.Contato) ? 0f : Medir(dados.Contato!, f.Contato).Height + 14;
                var hLoja = Vazio(dados.LojaNome) ? 0f : Medir(dados.LojaNome, f.Loja).Height;
                var hMensagem = Vazio(dados.Mensagem) ? 0f : Medir(dados.Mensagem!, f.Mensagem).Height + 30;
                var yb = yRodape - hContato - hLoja - hMensagem;
                if (hMensagem > 0)
                {
                    Escrever(ctx, dados.Mensagem!, f.Mensagem, Branco, centro, yb);
                    yb += hMensagem;
                }
                if (hLoja > 0)
                {
                    Escrever(ctx, dados.LojaNome, f.Loja, Branco, centro, yb);
                    yb += hLoja;
                }
                if (hContato > 0)
                    Escrever(ctx, dados.Contato!, f.Contato, CinzaClaro, centro, yb + 14);
            });
            return arte;
        }

        /// <summary>9 — fundo liso escuro, foto inteira, texto a esquerda.</summary>
        private static Image<Rgba32> Minimalista(Image foto, StoryArteDados dados)
        {
            var f = FontesArte.Criar(0.95f);
            var destaque = ParseCor(dados.CorDestaque);
            const int margemX = 70;
            var bloco = Bloco.Medir(dados, f, Largura - 2 * margemX, esquerda: true);

            var disponivel = Altura - 2 * MargemSegura;
            var alturaFoto = (int)Math.Clamp(disponivel - bloco.Altura - 70, 380, 820);
            using var imagem = foto.CloneAs<Rgba32>();
            imagem.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(Largura, alturaFoto) }));

            var total = imagem.Height + 70 + bloco.Altura;
            var y = MargemSegura + Math.Max(0, (disponivel - total) / 2f);

            var arte = new Image<Rgba32>(Largura, Altura, Color.ParseHex("#0F1115").ToPixel<Rgba32>());
            var cores = new Cores(Branco, Color.ParseHex("#9AA4B2"), Pilula: destaque, TextoPilula: Branco, Barra: destaque, Risco: destaque);
            arte.Mutate(ctx =>
            {
                ctx.DrawImage(imagem, new Point((Largura - imagem.Width) / 2, (int)y), 1f);
                y += imagem.Height + 70;
                ctx.Fill(destaque, new RectangularPolygon(margemX - 26, y + 6, 8, bloco.Altura - 12));
                bloco.Desenhar(ctx, dados, f, margemX, y, cores, esquerda: true);
            });
            return arte;
        }

        // ------------------------------------------------------------------
        // Bloco de texto dos layouts 2 a 10
        // ------------------------------------------------------------------

        private sealed record Cores(Color Texto, Color Secundario, Color Pilula, Color TextoPilula, Color Barra, Color Risco)
        {
            public static Cores SobreEscuro(Color destaque)
                => new(Branco, CinzaClaro, destaque, Luminancia(destaque) > 0.6 ? Escuro : Branco, destaque, destaque);
        }

        private sealed class FontesArte
        {
            public Font Selo = null!, Titulo = null!, Detalhes = null!, Anterior = null!, Preco = null!,
                Mensagem = null!, Loja = null!, Contato = null!;
            public float Escala;

            public static FontesArte Criar(float escala)
            {
                var (regular, semiBold, extraBold) = Familias.Value;
                return new FontesArte
                {
                    Escala = escala,
                    Selo = extraBold.CreateFont(46 * escala),
                    Titulo = extraBold.CreateFont(72 * escala),
                    Detalhes = regular.CreateFont(40 * escala),
                    Anterior = semiBold.CreateFont(44 * escala),
                    Preco = extraBold.CreateFont(112 * escala),
                    Mensagem = semiBold.CreateFont(52 * escala),
                    Loja = semiBold.CreateFont(46 * escala),
                    Contato = regular.CreateFont(38 * escala)
                };
            }
        }

        /// <summary>
        /// Selo, titulo, detalhes, preco e loja, um embaixo do outro. Mede antes
        /// para o layout decidir onde o bloco comeca.
        /// </summary>
        private sealed class Bloco
        {
            private float _hSelo, _hTitulo, _hDetalhes, _hAnterior, _hPreco, _hMensagem, _hLoja, _hContato, _largura;
            private bool _seloTexto;
            private const float GapSelo = 34, GapPreco = 36, GapLoja = 54;

            public float Altura { get; private set; }

            public static Bloco Medir(StoryArteDados d, FontesArte f, float largura, bool esquerda = false)
            {
                float E(float v) => v * f.Escala;
                var b = new Bloco { _largura = largura, _seloTexto = esquerda };
                var selo = d.Selo.ToUpperInvariant();
                if (!Vazio(selo))
                    b._hSelo = StoryArte.Medir(selo, f.Selo, largura).Height + (esquerda ? 0 : E(40));
                b._hTitulo = StoryArte.Medir(d.Titulo, f.Titulo, largura).Height;
                if (!Vazio(d.Detalhes)) b._hDetalhes = StoryArte.Medir(d.Detalhes!, f.Detalhes, largura).Height + E(12);
                if (!Vazio(d.PrecoAnterior)) b._hAnterior = StoryArte.Medir(d.PrecoAnterior!, f.Anterior, largura).Height + E(12);
                if (!Vazio(d.Preco)) b._hPreco = StoryArte.Medir(d.Preco!, f.Preco, largura).Height;
                if (!Vazio(d.Mensagem)) b._hMensagem = StoryArte.Medir(d.Mensagem!, f.Mensagem, largura).Height;
                if (!Vazio(d.LojaNome)) b._hLoja = StoryArte.Medir(d.LojaNome, f.Loja, largura).Height;
                if (!Vazio(d.Contato)) b._hContato = StoryArte.Medir(d.Contato!, f.Contato, largura).Height + E(10);

                var meio = b._hAnterior + b._hPreco + b._hMensagem;
                b.Altura = b._hSelo + (b._hSelo > 0 ? E(GapSelo) : 0)
                         + b._hTitulo + b._hDetalhes
                         + (meio > 0 ? E(GapPreco) + meio : 0)
                         + (b._hLoja + b._hContato > 0 ? E(GapLoja) + b._hLoja + b._hContato : 0);
                return b;
            }

            /// <param name="x">Centro do bloco, ou a borda esquerda quando <paramref name="esquerda"/>.</param>
            public void Desenhar(IImageProcessingContext ctx, StoryArteDados d, FontesArte f, float x, float y, Cores c, bool esquerda = false)
            {
                float E(float v) => v * f.Escala;
                void Texto(string t, Font fonte, Color cor, float topo)
                    => Escrever(ctx, t, fonte, cor, x, topo, esquerda, _largura);

                if (_hSelo > 0)
                {
                    var selo = d.Selo.ToUpperInvariant();
                    if (_seloTexto)
                    {
                        Texto(selo, f.Selo, c.Pilula, y);
                    }
                    else
                    {
                        var w = StoryArte.Medir(selo, f.Selo, _largura).Width + E(80);
                        DesenharPilula(ctx, c.Pilula, esquerda ? x : x - w / 2, y, w, _hSelo);
                        Escrever(ctx, selo, f.Selo, c.TextoPilula, esquerda ? x + w / 2 : x, y + E(20));
                    }
                    y += _hSelo + E(GapSelo);
                }

                Texto(d.Titulo, f.Titulo, c.Texto, y);
                y += _hTitulo;

                if (_hDetalhes > 0)
                {
                    Texto(d.Detalhes!, f.Detalhes, c.Secundario, y + E(12));
                    y += _hDetalhes;
                }

                if (_hAnterior + _hPreco + _hMensagem > 0) y += E(GapPreco);

                if (_hAnterior > 0)
                {
                    PrecoRiscado(ctx, d.PrecoAnterior!, f.Anterior, c.Secundario, c.Risco, x, y, esquerda);
                    y += _hAnterior;
                }
                if (_hPreco > 0)
                {
                    Texto(d.Preco!, f.Preco, c.Texto, y);
                    y += _hPreco;
                }
                if (_hMensagem > 0)
                {
                    Texto(d.Mensagem!, f.Mensagem, c.Texto, y);
                    y += _hMensagem;
                }

                if (_hLoja + _hContato > 0)
                {
                    y += E(GapLoja);
                    if (_hLoja > 0)
                    {
                        var barraX = esquerda ? x : x - E(60);
                        ctx.Fill(c.Barra, new RectangularPolygon(barraX, y - E(24), E(120), 6));
                        Texto(d.LojaNome, f.Loja, c.Texto, y);
                        y += _hLoja;
                    }
                    if (_hContato > 0)
                        Texto(d.Contato!, f.Contato, c.Secundario, y + E(10));
                }
            }
        }

        // ------------------------------------------------------------------
        // Utilitarios de desenho
        // ------------------------------------------------------------------

        private static byte[] ParaJpeg(Image arte)
        {
            using var ms = new MemoryStream();
            arte.SaveAsJpeg(ms, new JpegEncoder { Quality = 88 });
            return ms.ToArray();
        }

        /// <summary>
        /// Foto em "cover" desfocada e escurecida. O desfoque roda numa versao
        /// pequena e depois amplia: em 1080x1920 ele levaria segundos na VM de 1
        /// OCPU, e o resultado visual e' o mesmo.
        /// </summary>
        private static Image<Rgba32> MontarFundo(Image foto, int largura, int altura)
        {
            var fundo = foto.CloneAs<Rgba32>();
            fundo.Mutate(x => x
                .Resize(new ResizeOptions { Mode = ResizeMode.Crop, Size = new Size(largura / 4, altura / 4) })
                .GaussianBlur(10)
                .Resize(largura, altura)
                .Fill(Color.Black.WithAlpha(0.62f)));
            return fundo;
        }

        /// <summary>Preenche exatamente largura x altura, cortando as sobras do centro.</summary>
        private static Image<Rgba32> Cobrir(Image foto, int largura, int altura)
        {
            var img = foto.CloneAs<Rgba32>();
            img.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Crop, Size = new Size(largura, altura) }));
            return img;
        }

        private static LinearGradientBrush Degrade(float yInicio, float yFim, Color de, Color para)
            => new(new PointF(0, yInicio), new PointF(0, yFim), GradientRepetitionMode.None,
                new ColorStop(0, de), new ColorStop(1, para));

        private static void DesenharPilula(IImageProcessingContext ctx, Color cor, float x, float y, float w, float h)
        {
            var r = h / 2;
            ctx.Fill(cor, new RectangularPolygon(x + r, y, w - 2 * r, h));
            ctx.Fill(cor, new EllipsePolygon(x + r, y + r, r));
            ctx.Fill(cor, new EllipsePolygon(x + w - r, y + r, r));
        }

        private static void RetanguloArredondado(IImageProcessingContext ctx, Color cor, float x, float y, float w, float h, float r)
        {
            ctx.Fill(cor, new RectangularPolygon(x + r, y, w - 2 * r, h));
            ctx.Fill(cor, new RectangularPolygon(x, y + r, w, h - 2 * r));
            ctx.Fill(cor, new EllipsePolygon(x + r, y + r, r));
            ctx.Fill(cor, new EllipsePolygon(x + w - r, y + r, r));
            ctx.Fill(cor, new EllipsePolygon(x + r, y + h - r, r));
            ctx.Fill(cor, new EllipsePolygon(x + w - r, y + h - r, r));
        }

        /// <summary>Preco antigo com um risco: e' o que faz o "baixou" ser lido de relance.</summary>
        private static void PrecoRiscado(IImageProcessingContext ctx, string texto, Font fonte, Color cor, Color risco,
            float x, float y, bool esquerda)
        {
            var tamanho = Medir(texto, fonte);
            Escrever(ctx, texto, fonte, cor, x, y, esquerda);
            var meio = y + tamanho.Height * 0.45f;
            var inicio = esquerda ? x - 8 : x - tamanho.Width / 2 - 8;
            ctx.DrawLine(risco, 5f, new PointF(inicio, meio), new PointF(inicio + tamanho.Width + 16, meio));
        }

        private static void Escrever(IImageProcessingContext ctx, string texto, Font fonte, Color cor, float x, float topo,
            bool esquerda = false, float largura = LarguraTexto)
        {
            ctx.DrawText(new RichTextOptions(fonte)
            {
                Origin = new PointF(x, topo),
                HorizontalAlignment = esquerda ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                TextAlignment = esquerda ? TextAlignment.Start : TextAlignment.Center,
                WrappingLength = largura
            }, texto, cor);
        }

        private static FontRectangle Medir(string texto, Font fonte, float largura = LarguraTexto)
            => TextMeasurer.MeasureSize(texto, new TextOptions(fonte) { WrappingLength = largura });

        private static bool Vazio(string? s) => string.IsNullOrWhiteSpace(s);

        private static double Luminancia(Color cor)
        {
            var p = cor.ToPixel<Rgba32>();
            return (0.299 * p.R + 0.587 * p.G + 0.114 * p.B) / 255;
        }

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
