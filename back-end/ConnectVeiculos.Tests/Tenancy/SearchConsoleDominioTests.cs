using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Infrastructure.Services.Seo;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Tenancy
{
    public class SearchConsoleDominioTests
    {
        [Fact]
        public void CodigoDaMetaTag_Tira_O_Content_Da_Meta_Do_Google()
        {
            var meta = "<meta name=\"google-site-verification\" content=\"pi6mGVRUMbkYVfLzCpEWuNPt_y6H-abc123\" />";

            SearchConsoleService.CodigoDaMetaTag(meta).Should().Be("pi6mGVRUMbkYVfLzCpEWuNPt_y6H-abc123");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("<meta name=\"google-site-verification\" />")]
        [InlineData("<meta content=\"\"><script>x</script>\" />")]
        public void CodigoDaMetaTag_Sem_Codigo_Valido_Devolve_Null(string? meta)
        {
            SearchConsoleService.CodigoDaMetaTag(meta).Should().BeNull();
        }

        [Fact]
        public void Trocar_O_Dominio_Zera_O_Cadastro_No_Search_Console()
        {
            var t = new Tenant("acme", "Acme Veiculos");
            t.SetDominioProprio("acme.com.br");
            t.AtivarDominioProprio();
            t.RegistrarCodigoSearchConsole("codigo-do-google-123");
            t.RegistrarNoSearchConsole();

            t.SetDominioProprio("outra.com.br");

            t.TenGoogleVerifAuto.Should().BeNull();
            t.TenSearchConsoleEm.Should().BeNull();
        }

        [Fact]
        public void Mesmo_Dominio_Mantem_O_Cadastro()
        {
            var t = new Tenant("acme", "Acme Veiculos");
            t.SetDominioProprio("acme.com.br");
            t.RegistrarCodigoSearchConsole("codigo-do-google-123");
            t.RegistrarNoSearchConsole();

            t.SetDominioProprio("acme.com.br");

            t.TenGoogleVerifAuto.Should().Be("codigo-do-google-123");
            t.TenSearchConsoleEm.Should().NotBeNull();
        }
    }
}
