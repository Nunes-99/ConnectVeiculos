using ConnectVeiculos.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Security
{
    /// <summary>
    /// O log nativo do HttpClient escrevia a URL inteira em nivel Information.
    /// Como as integracoes passam credencial na query, o segredo do app da Meta
    /// e os tokens de cada tenant ficavam em texto puro no log do container.
    /// As URLs abaixo sao as que vazaram de verdade, com os valores trocados.
    /// </summary>
    public class RedacaoDeSegredosHandlerTests
    {
        [Fact]
        public void DeveEsconderOSegredoDoAppEOCodigoDeAutorizacao()
        {
            var url = "https://graph.facebook.com/v18.0/oauth/access_token"
                    + "?client_id=1046429011496802&client_secret=abc123def456&code=AQLsBl51HdCt";

            var redigida = RedacaoDeSegredosHandler.Redigir(url);

            redigida.Should().NotContain("abc123def456");
            redigida.Should().NotContain("AQLsBl51HdCt");
            redigida.Should().Contain("client_secret=***");
            redigida.Should().Contain("code=***");
        }

        [Fact]
        public void DeveEsconderOTokenDeAcesso()
        {
            var url = "https://graph.facebook.com/v18.0/me/accounts?fields=id,name&access_token=EAAO3uL2DT2IB";

            RedacaoDeSegredosHandler.Redigir(url).Should().Be(
                "https://graph.facebook.com/v18.0/me/accounts?fields=id,name&access_token=***");
        }

        [Fact]
        public void DeveManterOQueAjudaADepurar()
        {
            // client_id identifica qual app esta chamando e nao e' segredo; o host
            // e o caminho sao o que torna o log util. So o valor sensivel sai.
            var redigida = RedacaoDeSegredosHandler.Redigir(
                "https://graph.facebook.com/v18.0/oauth/access_token?client_id=1046429011496802&client_secret=xyz");

            redigida.Should().Contain("graph.facebook.com/v18.0/oauth/access_token");
            redigida.Should().Contain("client_id=1046429011496802");
        }

        [Fact]
        public void NaoDeveMexerEmUrlSemSegredo()
        {
            const string url = "https://connectveiculos.dev.br/api/feed/facebook?tenant=empresa-teste";

            RedacaoDeSegredosHandler.Redigir(url).Should().Be(url);
        }

        [Theory]
        [InlineData("refresh_token")]
        [InlineData("fb_exchange_token")]
        [InlineData("api_key")]
        [InlineData("password")]
        [InlineData("signature")]
        public void DeveCobrirOsDemaisParametrosSensiveis(string parametro)
        {
            var redigida = RedacaoDeSegredosHandler.Redigir($"https://x.com/a?{parametro}=valorsecreto");

            redigida.Should().NotContain("valorsecreto");
            redigida.Should().Be($"https://x.com/a?{parametro}=***");
        }

        [Fact]
        public void DeveEsconderTodosQuandoHaVariosNaMesmaUrl()
        {
            var redigida = RedacaoDeSegredosHandler.Redigir(
                "https://x.com/a?access_token=aaa&refresh_token=bbb&client_secret=ccc");

            redigida.Should().NotContain("aaa").And.NotContain("bbb").And.NotContain("ccc");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void NaoDeveQuebrarComUrlAusente(string? url)
        {
            RedacaoDeSegredosHandler.Redigir(url).Should().BeEmpty();
        }
    }
}
