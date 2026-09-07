using ConnectVeiculos.Infrastructure.Cache;
using ConnectVeiculos.Infrastructure.Hubs;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// O catalogo publico e cacheado por 1 minuto no CatalogoController e nada
    /// invalidava esse cache. O SignalR avisava na hora, o navegador refazia a
    /// busca na hora, e a API devolvia a lista velha: carro novo levava ate 1
    /// minuto pra aparecer, com o selo "Em tempo real" na tela.
    /// </summary>
    public class CatalogoHubServiceTests
    {
        private readonly Mock<ICacheService> _cacheMock = new();
        private readonly Mock<IHubContext<CatalogoHub>> _hubMock = new();

        private CatalogoHubService CriarServico()
        {
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
            _hubMock.SetupGet(h => h.Clients).Returns(clients.Object);

            return new CatalogoHubService(_hubMock.Object, _cacheMock.Object);
        }

        [Fact]
        public async Task NotificarAtualizacaoCatalogo_DeveInvalidarOCacheDoCatalogo()
        {
            await CriarServico().NotificarAtualizacaoCatalogo("acme", 1, "VEICULO_ADICIONADO", new { });

            _cacheMock.Verify(c => c.RemoveByPrefix(CacheKeys.Catalogo), Times.Once);
        }

        [Fact]
        public async Task NotificarAtualizacaoCatalogo_DeveInvalidarAntesDeAvisarOsClients()
        {
            // A ordem importa: os clients refazem a busca assim que recebem o
            // evento. Invalidar depois deixaria essa primeira busca pegar o cache
            // velho — que e' exatamente o bug original.
            var ordem = new List<string>();
            _cacheMock.Setup(c => c.RemoveByPrefix(It.IsAny<string>()))
                      .Callback(() => ordem.Add("cache"));

            var proxy = new Mock<IClientProxy>();
            proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                 .Callback(() => ordem.Add("signalr"))
                 .Returns(Task.CompletedTask);

            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
            _hubMock.SetupGet(h => h.Clients).Returns(clients.Object);

            await new CatalogoHubService(_hubMock.Object, _cacheMock.Object)
                .NotificarAtualizacaoCatalogo("acme", 1, "VEICULO_ADICIONADO", new { });

            ordem.Should().NotBeEmpty();
            ordem[0].Should().Be("cache");
            ordem.Should().Contain("signalr");
        }

        [Theory]
        [InlineData("VEICULO_ADICIONADO")]
        [InlineData("VEICULO_REMOVIDO")]
        [InlineData("VEICULO_VENDIDO")]
        public async Task QualquerEventoDeCatalogo_DeveInvalidarOCache(string evento)
        {
            await CriarServico().NotificarAtualizacaoCatalogo("acme", 1, evento, new { });

            _cacheMock.Verify(c => c.RemoveByPrefix(CacheKeys.Catalogo), Times.Once);
        }

        [Fact]
        public async Task DeveAvisarOGrupoDaLojaEOGrupoGeralDoTenant()
        {
            var grupos = new List<string>();
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Group(It.IsAny<string>()))
                   .Callback<string>(g => grupos.Add(g))
                   .Returns(Mock.Of<IClientProxy>());
            _hubMock.SetupGet(h => h.Clients).Returns(clients.Object);

            await new CatalogoHubService(_hubMock.Object, _cacheMock.Object)
                .NotificarAtualizacaoCatalogo("Acme", 7, "VEICULO_ADICIONADO", new { });

            grupos.Should().Contain("tenant_acme_catalogo_loja_7");
            grupos.Should().Contain("tenant_acme_catalogo_geral");
        }
    }
}
