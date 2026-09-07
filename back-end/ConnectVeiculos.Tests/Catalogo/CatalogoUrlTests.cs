using ConnectVeiculos.Core.Catalogo;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Catalogo
{
    public class CatalogoUrlTests
    {
        [Fact]
        public void Veiculo_DeveUsarOSlugDoTenantNaRota()
        {
            CatalogoUrl.Veiculo("https://connectveiculos.dev.br", "acme", 42)
                .Should().Be("https://connectveiculos.dev.br/catalogo/acme/veiculo/42");
        }

        [Theory]
        [InlineData("https://x.com.br/")]
        [InlineData("https://x.com.br///")]
        [InlineData("  https://x.com.br  ")]
        public void Veiculo_DeveNormalizarBarraEEspacoNaBaseUrl(string baseUrl)
        {
            CatalogoUrl.Veiculo(baseUrl, "acme", 7)
                .Should().Be("https://x.com.br/catalogo/acme/veiculo/7");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Veiculo_SemSlug_DeveCairNoCatalogoRaiz(string slug)
        {
            var url = CatalogoUrl.Veiculo("https://x.com.br", slug, 7);

            url.Should().Be("https://x.com.br/catalogo");
            // A rota /catalogo/veiculo/{id} nao existe no router — era o bug.
            url.Should().NotContain("/catalogo/veiculo/");
        }

        [Fact]
        public void Veiculo_DeveEscaparSlugComCaractereEspecial()
        {
            CatalogoUrl.Veiculo("https://x.com.br", "loja do joão", 1)
                .Should().Be("https://x.com.br/catalogo/loja%20do%20jo%C3%A3o/veiculo/1");
        }

        [Fact]
        public void Listagem_DeveMontarUrlDoCatalogoDoTenant()
        {
            CatalogoUrl.Listagem("https://x.com.br", "acme")
                .Should().Be("https://x.com.br/catalogo/acme");
        }

        [Fact]
        public void Listagem_SemSlug_DeveCairNoCatalogoRaiz()
        {
            CatalogoUrl.Listagem("https://x.com.br", null)
                .Should().Be("https://x.com.br/catalogo");
        }
    }
}
