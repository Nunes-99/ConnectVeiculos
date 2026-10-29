using System.Dynamic;
using ConnectVeiculos.Application.UseCases.Veiculos;
using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases
{
    /// <summary>
    /// O icone de story da lista fica colorido enquanto o story esta no ar:
    /// 24h depois do ultimo, igual ao proprio story.
    /// </summary>
    public class StoryNoArNaListaTests
    {
        private const int VeiculoId = 5;

        [Fact]
        public async Task Story_de_agora_fica_no_ar_por_24h_em_utc()
        {
            var lista = await ConsultarCom(Story("InstagramStory", DateTime.UtcNow.AddHours(-1)));

            var ate = lista.Single().StoryNoArAte;
            ate.Should().NotBeNull();
            ate!.Value.Kind.Should().Be(DateTimeKind.Utc);
            ate.Value.Should().BeCloseTo(DateTime.UtcNow.AddHours(23), TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task Vale_o_story_mais_recente_entre_instagram_e_facebook()
        {
            var lista = await ConsultarCom(
                Story("InstagramStory", DateTime.UtcNow.AddHours(-20)),
                Story("FacebookStory", DateTime.UtcNow.AddHours(-2)));

            lista.Single().StoryNoArAte!.Value.Should().BeCloseTo(DateTime.UtcNow.AddHours(22), TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task Story_de_mais_de_24h_ja_sumiu()
        {
            var lista = await ConsultarCom(Story("InstagramStory", DateTime.UtcNow.AddHours(-25)));

            lista.Single().StoryNoArAte.Should().BeNull();
        }

        [Fact]
        public async Task Post_do_feed_nao_conta_como_story()
        {
            var lista = await ConsultarCom(Story("Instagram", DateTime.UtcNow.AddHours(-1)));

            lista.Single().StoryNoArAte.Should().BeNull();
        }

        private static VeiculoPublicacao Story(string plataforma, DateTime em)
        {
            var p = new VeiculoPublicacao(VeiculoId, plataforma, "x", "https://exemplo");
            // O banco devolve a data sem fuso (Kind Unspecified), como o SQLite.
            typeof(VeiculoPublicacao).GetProperty(nameof(VeiculoPublicacao.PubDtPublicacao))!
                .SetValue(p, DateTime.SpecifyKind(em, DateTimeKind.Unspecified));
            return p;
        }

        private static async Task<List<Application.ViewModels.Veiculos.VeiculoViewModel>> ConsultarCom(params VeiculoPublicacao[] publicacoes)
        {
            dynamic linha = new ExpandoObject();
            linha.VeiId = (long)VeiculoId;
            linha.R_LojId = 1L;
            linha.LojaNome = "Loja";
            linha.R_CatId = 1L;
            linha.CategoriaNome = "Carro";
            linha.VeiMarca = "VW";
            linha.VeiModelo = "Gol";
            linha.VeiAno = 2020L;
            linha.VeiPlaca = "ABC1D23";
            linha.VeiChassi = "9BW";
            linha.VeiRenavam = "1";
            linha.VeiCor = "Prata";
            linha.VeiKm = 1000L;
            linha.VeiPreco = 50000d;
            linha.VeiDtEntrada = DateTime.Today;
            linha.VeiSts = "D";
            linha.VeiSitSts = "A";
            linha.VeiPrecoCompra = 40000d;
            linha.VeiObservacao = null;
            linha.VeiOpcionais = null;
            linha.VeiDonoAtual = null;
            linha.VeiDonoCelular = null;
            linha.VeiPrecoFipe = null;
            linha.VeiPostadoInsta = 0L;
            linha.VeiPostadoFace = 0L;
            linha.VeiDtPostagemInsta = null;
            linha.VeiDtPostagemFace = null;

            var operations = new Mock<IVeiculoOperations>();
            operations.Setup(o => o.ConsultarVisualizacaoVeiculos(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((object)new List<dynamic> { linha });
            var publicacaoRepo = new Mock<IVeiculoPublicacaoRepository>();
            publicacaoRepo.Setup(r => r.GetAtivasAsync()).ReturnsAsync(publicacoes);

            return await new ConsultarVeiculosUseCase(operations.Object, publicacaoRepo.Object)
                .Execute(null!, null, null!, null!);
        }
    }
}
