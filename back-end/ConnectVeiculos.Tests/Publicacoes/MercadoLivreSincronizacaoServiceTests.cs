using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.MercadoLivre;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Publicacoes
{
    /// <summary>
    /// A conta do Mercado Livre cai a cada 6 horas: o aplicativo nao recebe
    /// refresh_token, entao o token expira e ninguem renova. Enquanto esta fora,
    /// todo veiculo cadastrado deixa de ser anunciado.
    ///
    /// Esta rotina e' o que recupera o atraso — roda ao clicar em sincronizar e,
    /// principalmente, sozinha depois de reconectar.
    /// </summary>
    public class MercadoLivreSincronizacaoServiceTests
    {
        private readonly Mock<IMercadoLivreService> _ml = new();
        private readonly Mock<IVeiculoRepository> _veiculos = new();
        private readonly Mock<IVeiculoPublicacaoRepository> _publicacoes = new();

        public MercadoLivreSincronizacaoServiceTests()
        {
            _ml.Setup(x => x.IsConnectedAsync()).ReturnsAsync(true);
            _ml.Setup(x => x.PublicarVeiculoAsync(It.IsAny<int>()))
               .ReturnsAsync(("MLB1", "https://ml/1", false));
        }

        [Fact]
        public async Task DevePublicarSomenteOsDisponiveis()
        {
            _veiculos.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
            {
                Veiculo(1, "D"), Veiculo(2, "V"), Veiculo(3, "D"), Veiculo(4, "I")
            });

            var r = await Criar().SincronizarDisponiveisAsync();

            r.TotalDisponiveis.Should().Be(2);
            r.NovosPublicados.Should().Be(2);
            _ml.Verify(x => x.PublicarVeiculoAsync(2), Times.Never, "veiculo vendido nao volta pro ar");
            _ml.Verify(x => x.PublicarVeiculoAsync(4), Times.Never, "veiculo inativo nao volta pro ar");
        }

        [Fact]
        public async Task NaoDeveDuplicarAnuncioDeVeiculoQueJaTemUm()
        {
            _veiculos.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { Veiculo(1, "D"), Veiculo(2, "D") });
            _publicacoes.Setup(x => x.GetAtivaByVeiculoEPlataformaAsync(1, "MercadoLivre"))
                        .ReturnsAsync(new VeiculoPublicacao(1, "MercadoLivre", "MLB-ja-existe", "https://ml/x"));

            var r = await Criar().SincronizarDisponiveisAsync();

            r.JaPublicados.Should().Be(1);
            r.NovosPublicados.Should().Be(1);
            _ml.Verify(x => x.PublicarVeiculoAsync(1), Times.Never);
        }

        [Fact]
        public async Task NaoDeveFazerNadaComAContaDesconectada()
        {
            // A rotina roda depois de reconectar, mas tambem pelo botao da tela —
            // e o token pode ter caido no meio do caminho.
            _ml.Setup(x => x.IsConnectedAsync()).ReturnsAsync(false);

            var r = await Criar().SincronizarDisponiveisAsync();

            r.TotalDisponiveis.Should().Be(0);
            _veiculos.Verify(x => x.GetAllAsync(), Times.Never);
        }

        [Fact]
        public async Task FalhaNumVeiculoNaoPodeInterromperOsOutros()
        {
            _veiculos.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
            {
                Veiculo(1, "D"), Veiculo(2, "D"), Veiculo(3, "D")
            });
            _ml.Setup(x => x.PublicarVeiculoAsync(2))
               .ThrowsAsync(new Exception("categoria exige listing_type_id"));

            var r = await Criar().SincronizarDisponiveisAsync();

            r.NovosPublicados.Should().Be(2);
            r.Falhas.Should().ContainSingle();
            r.Falhas[0].VeiculoId.Should().Be(2);
            r.Falhas[0].Erro.Should().Contain("listing_type_id");
            r.Falhas[0].Descricao.Should().Contain("Corolla");
        }

        [Fact]
        public async Task DeveContarOsQueFicaramAguardandoPagamento()
        {
            // O ML aceita o anuncio mas o deixa invisivel ate a taxa ser paga.
            // Sem este numero a tela dizia "publicado" pra anuncio nenhum no ar.
            _veiculos.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { Veiculo(1, "D") });
            _ml.Setup(x => x.PublicarVeiculoAsync(1))
               .ReturnsAsync(("MLB9", "https://ml/9", true));

            var r = await Criar().SincronizarDisponiveisAsync();

            r.AguardandoPagamento.Should().Be(1);
        }

        [Fact]
        public async Task DeveRegistrarAPublicacaoParaNaoRepublicarNaProximaVez()
        {
            _veiculos.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { Veiculo(1, "D") });
            var salvas = new List<VeiculoPublicacao>();
            _publicacoes.Setup(x => x.CreateAsync(It.IsAny<VeiculoPublicacao>()))
                        .Callback<VeiculoPublicacao>(salvas.Add)
                        .ReturnsAsync(1);

            await Criar().SincronizarDisponiveisAsync();

            salvas.Should().ContainSingle();
            salvas[0].PubPlataforma.Should().Be("MercadoLivre");
            salvas[0].PubExternoId.Should().Be("MLB1");
        }

        private MercadoLivreSincronizacaoService Criar() => new(
            _ml.Object, _veiculos.Object, _publicacoes.Object,
            NullLogger<MercadoLivreSincronizacaoService>.Instance);

        private static Veiculo Veiculo(int id, string status) => new(
            id, 1, 1, "Toyota", "Corolla", 2024, "ABC1D23", "9BWZZZ377VT004251",
            "Branco", 10000, 145000m, DateTime.Now, status, "D", 130000m);
    }
}
