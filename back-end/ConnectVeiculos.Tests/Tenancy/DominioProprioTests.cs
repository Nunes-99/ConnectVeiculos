using ConnectVeiculos.API.Middlewares;
using ConnectVeiculos.Core.Catalogo;
using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Tenancy
{
    public class DominioProprioTests
    {
        [Theory]
        [InlineData("diamanteveiculos.com.br", "diamanteveiculos.com.br")]
        [InlineData("  DiamanteVeiculos.COM.br ", "diamanteveiculos.com.br")]
        [InlineData("https://www.diamanteveiculos.com.br/catalogo?x=1", "diamanteveiculos.com.br")]
        [InlineData("http://diamanteveiculos.com.br:443", "diamanteveiculos.com.br")]
        [InlineData("www.diamanteveiculos.com.br.", "diamanteveiculos.com.br")]
        [InlineData("loja.diamante.com", "loja.diamante.com")]
        [InlineData("veículos.com.br", "xn--veculos-8ya.com.br")]
        public void Normalizar_DeveChegarNaFormaGuardada(string entrada, string esperado)
        {
            DominioProprio.Normalizar(entrada).Should().Be(esperado);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("https://")]
        public void Normalizar_Vazio_DeveDarNull(string? entrada)
        {
            DominioProprio.Normalizar(entrada).Should().BeNull();
        }

        [Theory]
        [InlineData("diamanteveiculos.com.br", true)]
        [InlineData("a-b.com", true)]
        [InlineData("xn--veculos-8ya.com.br", true)]
        [InlineData("localhost", false)]
        [InlineData("136.248.77.154", false)]
        [InlineData("-loja.com.br", false)]
        [InlineData("loja..com.br", false)]
        [InlineData("loja.com.br/../etc", false)]
        [InlineData("loja com.br", false)]
        public void EhValido(string dominio, bool esperado)
        {
            DominioProprio.EhValido(dominio).Should().Be(esperado);
        }

        [Theory]
        [InlineData("connectveiculos.dev.br", true)]
        [InlineData("diamante.connectveiculos.dev.br", true)]
        [InlineData("connectveiculos.com.br", true)]
        [InlineData("meuconnectveiculos.dev.br", false)]
        [InlineData("diamanteveiculos.com.br", false)]
        public void EhDaPlataforma(string dominio, bool esperado)
        {
            DominioProprio.EhDaPlataforma(dominio).Should().Be(esperado);
        }

        [Fact]
        public void Tenant_DominioNovo_FicaPendenteAteAtivar()
        {
            var t = new Tenant("empresa-teste", "Diamante");
            t.SetDominioProprio("diamanteveiculos.com.br");
            t.DominioProprioAtivo().Should().BeFalse();

            t.AtivarDominioProprio();
            t.DominioProprioAtivo().Should().BeTrue();

            // Trocar o dominio volta a esperar o certificado do novo.
            t.SetDominioProprio("outrodominio.com.br");
            t.DominioProprioAtivo().Should().BeFalse();
            t.TenDominioAtivoEm.Should().BeNull();
        }

        [Fact]
        public void Tenant_MesmoDominio_NaoPerdeAtivacao()
        {
            var t = new Tenant("empresa-teste", "Diamante");
            t.SetDominioProprio("diamanteveiculos.com.br");
            t.AtivarDominioProprio();

            t.SetDominioProprio("diamanteveiculos.com.br");
            t.DominioProprioAtivo().Should().BeTrue();
        }

        [Fact]
        public void Tenant_SemDominio_NuncaAtivo()
        {
            var t = new Tenant("empresa-teste", "Diamante");
            t.AtivarDominioProprio();
            t.DominioProprioAtivo().Should().BeFalse();
        }

        [Theory]
        [InlineData("diamanteveiculos.com.br")]
        [InlineData("www.diamanteveiculos.com.br")]
        public async Task Middleware_HostDoDominioProprio_ResolveOTenantDaLoja(string host)
        {
            var diamante = new Tenant("empresa-teste", "Diamante");
            var store = new Mock<ITenantStore>();
            store.Setup(s => s.GetByDominioAsync(host, It.IsAny<CancellationToken>())).ReturnsAsync(diamante);
            store.Setup(s => s.GetBySlugAsync("empresa-teste", It.IsAny<CancellationToken>())).ReturnsAsync(diamante);

            var tenantContext = CriarTenantContext();
            var http = new DefaultHttpContext();
            http.Request.Host = new HostString(host);
            http.Request.Path = "/api/catalogo";

            var chamouProximo = false;
            var mw = new TenantResolutionMiddleware(_ => { chamouProximo = true; return Task.CompletedTask; },
                NullLogger<TenantResolutionMiddleware>.Instance);
            await mw.InvokeAsync(http, store.Object, tenantContext);

            chamouProximo.Should().BeTrue();
            tenantContext.TenantSlug.Should().Be("empresa-teste");
        }

        [Fact]
        public async Task Middleware_QueryTenant_AindaVenceODominio()
        {
            var diamante = new Tenant("empresa-teste", "Diamante");
            var outra = new Tenant("outra", "Outra");
            var store = new Mock<ITenantStore>();
            store.Setup(s => s.GetByDominioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(diamante);
            store.Setup(s => s.GetBySlugAsync("outra", It.IsAny<CancellationToken>())).ReturnsAsync(outra);

            var tenantContext = CriarTenantContext();
            var http = new DefaultHttpContext();
            http.Request.Host = new HostString("diamanteveiculos.com.br");
            http.Request.Path = "/api/catalogo";
            http.Request.QueryString = new QueryString("?tenant=outra");

            var mw = new TenantResolutionMiddleware(_ => Task.CompletedTask, NullLogger<TenantResolutionMiddleware>.Instance);
            await mw.InvokeAsync(http, store.Object, tenantContext);

            tenantContext.TenantSlug.Should().Be("outra");
        }

        private static ITenantContext CriarTenantContext()
        {
            var infraAssembly = typeof(ConnectVeiculos.Infrastructure.IoC.DependencyInjectionExtensions).Assembly;
            var type = infraAssembly.GetType("ConnectVeiculos.Infrastructure.Tenancy.TenantContext", throwOnError: true)!;
            return (ITenantContext)Activator.CreateInstance(type, nonPublic: true)!;
        }
    }
}
