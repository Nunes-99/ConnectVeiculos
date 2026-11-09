using System.Net;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Services.Meta;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Publicacoes
{
    /// <summary>Post na Page do Facebook: uma foto vai por /photos; varias, por /feed com attached_media.</summary>
    public class FacebookPagePostServiceTests
    {
        private const int VeiculoId = 7;

        private sealed class GraphFalso : HttpMessageHandler
        {
            public readonly List<(string Url, JsonElement Corpo)> Chamadas = new();
            public Func<string, JsonElement, (HttpStatusCode, string)> Responder = (_, _) => (HttpStatusCode.OK, "{}");
            private int _foto;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var corpo = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
                var url = request.RequestUri!.ToString();
                Chamadas.Add((url, corpo));
                var (status, json) = Responder(url, corpo);
                return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }

            public string ProximaFoto() => $"{{\"id\":\"foto{++_foto}\"}}";
        }

        private static (FacebookPagePostService Service, GraphFalso Graph) Criar(int qtdFotos,
            ConnectVeiculos.Core.Interfaces.Services.IRedesPersonalizacaoService? personalizacao = null)
        {
            var graph = new GraphFalso();
            var config = new Mock<IConfiguracaoSistemaRepository>();
            config.Setup(x => x.GetValorAsync(MetaOAuthService.KEY_PAGE_ID)).ReturnsAsync("123");
            config.Setup(x => x.GetValorAsync(MetaOAuthService.KEY_PAGE_TOKEN_CIFRADO)).ReturnsAsync("tok");
            var protector = new Mock<ITokenProtector>();
            protector.Setup(x => x.Unprotect("tok")).Returns("tok");

            var veiculo = new Veiculo(VeiculoId, 1, 1, "VW", "Gol", 2012, "ABC1D23", "", "Prata", 98000, 31900m, DateTime.Today, "D", "", 0m, "");
            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(x => x.GetByIdAsync(VeiculoId)).ReturnsAsync(veiculo);
            var imagens = new Mock<IVeiculoImagemRepository>();
            imagens.Setup(x => x.GetByVeiculoIdAsync(VeiculoId)).ReturnsAsync(
                Enumerable.Range(1, qtdFotos).Select(i => new VeiculoImagem(i, VeiculoId, $"/uploads/f{i}.jpg", qtdFotos - i, true)).ToList());
            var lojas = new Mock<ILojaRepository>();
            lojas.Setup(x => x.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Loja?)null);
            var tenant = new Mock<ITenantContext>();

            var service = new FacebookPagePostService(new HttpClient(graph),
                Options.Create(new MetaSettings { ApiVersion = "v18.0", PublicSiteUrl = "https://connectveiculos.dev.br" }),
                config.Object, protector.Object, veiculos.Object, imagens.Object, lojas.Object,
                NullLogger<FacebookPagePostService>.Instance, tenant.Object, personalizacao);
            return (service, graph);
        }

        [Fact]
        public async Task Uma_Foto_Publica_Direto_Em_Photos()
        {
            var (service, graph) = Criar(1);
            graph.Responder = (_, _) => (HttpStatusCode.OK, "{\"id\":\"foto1\",\"post_id\":\"123_9\"}");

            var r = await service.PublicarManualAsync(VeiculoId);

            r!.ExternoId.Should().Be("123_9");
            graph.Chamadas.Should().ContainSingle();
            graph.Chamadas[0].Url.Should().EndWith("/123/photos");
            graph.Chamadas[0].Corpo.GetProperty("message").GetString().Should().Contain("Gol");
        }

        [Fact]
        public async Task Varias_Fotos_Sobem_Sem_Publicar_E_Viram_Um_Post_No_Feed()
        {
            var (service, graph) = Criar(3);
            graph.Responder = (url, _) => url.EndsWith("/feed") ? (HttpStatusCode.OK, "{\"id\":\"123_42\"}") : (HttpStatusCode.OK, graph.ProximaFoto());

            var r = await service.PublicarManualAsync(VeiculoId);

            r!.ExternoId.Should().Be("123_42");
            r.Url.Should().Be("https://www.facebook.com/123_42");
            var fotos = graph.Chamadas.Where(c => c.Url.EndsWith("/photos")).ToList();
            fotos.Should().HaveCount(3);
            fotos.Should().OnlyContain(c => !c.Corpo.GetProperty("published").GetBoolean());
            // Na ordem das fotos do cadastro (ImgOrdem): f3 tem ordem 0.
            fotos[0].Corpo.GetProperty("url").GetString().Should().Contain("f3.jpg");

            var feed = graph.Chamadas.Single(c => c.Url.EndsWith("/123/feed")).Corpo;
            feed.GetProperty("message").GetString().Should().Contain("Gol");
            feed.GetProperty("attached_media").EnumerateArray().Select(m => m.GetProperty("media_fbid").GetString())
                .Should().Equal("foto1", "foto2", "foto3");
        }

        [Fact]
        public async Task Capa_Entra_Primeiro_E_A_Legenda_Segue_O_Modelo_Da_Loja()
        {
            var personalizacao = new Mock<ConnectVeiculos.Core.Interfaces.Services.IRedesPersonalizacaoService>();
            personalizacao.Setup(x => x.CapaDoPostAsync(VeiculoId, It.IsAny<string>()))
                          .ReturnsAsync("https://connectveiculos.dev.br/api/imagens/file?path=capa.jpg");
            personalizacao.Setup(x => x.ModeloLegendaAsync("facebook")).ReturnsAsync("Oferta: {modelo} por {preco}");
            var (service, graph) = Criar(14, personalizacao.Object);
            graph.Responder = (url, _) => url.EndsWith("/feed") ? (HttpStatusCode.OK, "{\"id\":\"123_42\"}") : (HttpStatusCode.OK, graph.ProximaFoto());

            await service.PublicarManualAsync(VeiculoId);

            var fotos = graph.Chamadas.Where(c => c.Url.EndsWith("/photos")).ToList();
            fotos.Should().HaveCount(10, "a capa ocupa um dos 10 lugares");
            fotos[0].Corpo.GetProperty("url").GetString().Should().Contain("capa.jpg");
            graph.Chamadas.Single(c => c.Url.EndsWith("/feed")).Corpo.GetProperty("message").GetString()
                 .Should().Be("Oferta: Gol por R$ 31.900,00");
        }

        [Fact]
        public async Task Legenda_Editada_Na_Hora_Vale_So_Para_Este_Post()
        {
            var personalizacao = new Mock<ConnectVeiculos.Core.Interfaces.Services.IRedesPersonalizacaoService>();
            personalizacao.Setup(x => x.ModeloLegendaAsync("facebook")).ReturnsAsync("Modelo da loja");
            var (service, graph) = Criar(1, personalizacao.Object);
            graph.Responder = (_, _) => (HttpStatusCode.OK, "{\"id\":\"foto1\",\"post_id\":\"123_9\"}");

            await service.PublicarManualAsync(VeiculoId, "Só hoje: {marca} {modelo}");

            graph.Chamadas[0].Corpo.GetProperty("message").GetString().Should().Be("Só hoje: VW Gol");
        }

        [Fact]
        public async Task Limita_A_10_Fotos()
        {
            var (service, graph) = Criar(14);
            graph.Responder = (url, _) => url.EndsWith("/feed") ? (HttpStatusCode.OK, "{\"id\":\"123_42\"}") : (HttpStatusCode.OK, graph.ProximaFoto());

            await service.PublicarManualAsync(VeiculoId);

            graph.Chamadas.Count(c => c.Url.EndsWith("/photos")).Should().Be(10);
            graph.Chamadas.Single(c => c.Url.EndsWith("/feed")).Corpo.GetProperty("attached_media").GetArrayLength().Should().Be(10);
        }

        [Fact]
        public async Task Se_O_Post_Com_Varias_Fotos_Falhar_Publica_So_A_Principal()
        {
            var (service, graph) = Criar(2);
            graph.Responder = (url, corpo) =>
                url.EndsWith("/feed") ? (HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"x\"}}")
                : corpo.TryGetProperty("published", out _) ? (HttpStatusCode.OK, graph.ProximaFoto())
                : (HttpStatusCode.OK, "{\"id\":\"f\",\"post_id\":\"123_1\"}");

            var r = await service.PublicarManualAsync(VeiculoId);

            r!.ExternoId.Should().Be("123_1");
            var ultima = graph.Chamadas.Last();
            ultima.Url.Should().EndWith("/photos");
            ultima.Corpo.TryGetProperty("published", out _).Should().BeFalse();
            ultima.Corpo.GetProperty("url").GetString().Should().Contain("f2.jpg"); // ordem 0
        }
    }
}
