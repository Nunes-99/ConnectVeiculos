using ConnectVeiculos.API.Middlewares;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Tenancy
{
    public class TenantSlugExtractionTests
    {
        [Theory]
        [InlineData("connectveiculos.dev.br", "default")]
        [InlineData("www.connectveiculos.dev.br", "default")]
        [InlineData("acme.connectveiculos.dev.br", "acme")]
        [InlineData("MotorsBR.connectveiculos.dev.br", "motorsbr")]
        [InlineData("demo.connectveiculos.dev.br", "demo")]
        public void Slug_Extraido_Corretamente_De_Hosts_Reais(string host, string expected)
        {
            TenantResolutionMiddleware.ExtractTenantSlug(host).Should().Be(expected);
        }

        [Theory]
        [InlineData("136.248.77.154")]
        [InlineData("127.0.0.1")]
        [InlineData("[::1]")]
        public void IP_Nu_Mapeia_Para_Default(string host)
        {
            TenantResolutionMiddleware.ExtractTenantSlug(host).Should().Be("default");
        }

        [Theory]
        [InlineData("localhost")]
        [InlineData("dev")]
        [InlineData("")]
        public void Localhost_E_Hosts_Sem_Ponto_Mapeiam_Para_Default(string host)
        {
            TenantResolutionMiddleware.ExtractTenantSlug(host).Should().Be("default");
        }

        [Fact]
        public void Subdomain_Multinivel_Pega_Primeiro_Nivel()
        {
            // a.b.connectveiculos.dev.br -> "a" (mais subdomains nao sao suportados,
            // mas o codigo nao quebra — primeiro nivel eh o tenant slug)
            TenantResolutionMiddleware.ExtractTenantSlug("a.b.connectveiculos.dev.br")
                .Should().Be("a");
        }
    }
}
