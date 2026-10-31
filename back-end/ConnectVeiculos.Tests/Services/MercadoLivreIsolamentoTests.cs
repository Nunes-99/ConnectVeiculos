using System.Net;
using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Services.MercadoLivre;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// 31/10/2026: uma loja recem-criada aparecia "Conectado como" a conta do
    /// Mercado Livre de outra. Os tokens eram gravados no IOptions.Value, que e'
    /// um objeto so para o processo inteiro.
    /// </summary>
    public class MercadoLivreIsolamentoTests
    {
        private sealed class Responde200 : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }

        private static MercadoLivreService Criar(IOptions<MercadoLivreSettings> opcoes, IntegracaoMercadoLivre? integracao)
        {
            var repo = new Mock<IIntegracaoMercadoLivreRepository>();
            repo.Setup(r => r.GetSingletonAsync()).ReturnsAsync(integracao);
            var protector = new Mock<ITokenProtector>();
            protector.Setup(p => p.Unprotect(It.IsAny<string>())).Returns((string s) => s);

            return new MercadoLivreService(
                new HttpClient(new Responde200()) { BaseAddress = new Uri("https://api.mercadolibre.com") },
                opcoes,
                NullLogger<MercadoLivreService>.Instance,
                Mock.Of<IVeiculoRepository>(),
                Mock.Of<IVeiculoImagemRepository>(),
                Mock.Of<ILojaRepository>(),
                Mock.Of<IConfiguracaoSistemaRepository>(),
                protector.Object,
                Mock.Of<IOAuthStateProtector>(),
                Mock.Of<ITenantContext>(),
                repo.Object,
                Mock.Of<IIntegracaoLogRepository>(),
                Mock.Of<IVeiculoPublicacaoRepository>());
        }

        [Fact]
        public async Task Loja_Sem_Integracao_Nao_Ve_O_Token_De_Outra_Loja()
        {
            // Mesmo IOptions para as duas, como no container de DI.
            var opcoes = Options.Create(new MercadoLivreSettings { AppId = "app", ClientSecret = "segredo", RedirectUri = "https://x" });

            var lojaConectada = Criar(opcoes, new IntegracaoMercadoLivre
            {
                IntAccessTokenCifrado = "token-da-loja-a",
                IntRefreshTokenCifrado = "refresh-da-loja-a",
                IntSellerId = "123",
                IntAccessTokenExpiraEm = DateTime.UtcNow.AddHours(5)
            });
            (await lojaConectada.IsConnectedAsync()).Should().BeTrue();

            var lojaNova = Criar(opcoes, null);
            (await lojaNova.IsConnectedAsync()).Should().BeFalse();
            opcoes.Value.AccessToken.Should().BeNull();
        }
    }
}
