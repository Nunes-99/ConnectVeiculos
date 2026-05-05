using ConnectVeiculos.Core.Interfaces.Tenancy;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Tenancy
{
    /// <summary>
    /// Testa a TenantContext (impl em Infrastructure.Tenancy). Como a classe eh
    /// internal, instanciamos via DI builder ou refletimos. Pra simplicidade,
    /// uso reflection — o tipo eh estavel.
    /// </summary>
    public class TenantContextTests
    {
        private static ITenantContext Create()
        {
            var infraAssembly = typeof(ConnectVeiculos.Infrastructure.IoC.DependencyInjectionExtensions).Assembly;
            var type = infraAssembly.GetType("ConnectVeiculos.Infrastructure.Tenancy.TenantContext", throwOnError: true)!;
            return (ITenantContext)Activator.CreateInstance(type, nonPublic: true)!;
        }

        [Fact]
        public void Construido_IsResolved_False()
        {
            var ctx = Create();
            ctx.IsResolved.Should().BeFalse();
            ctx.TenantId.Should().Be(0);
            ctx.TenantSlug.Should().BeEmpty();
            ctx.DatabaseFile.Should().BeEmpty();
        }

        [Fact]
        public void Resolve_Popula_Campos()
        {
            var ctx = Create();
            ctx.Resolve(7, "acme", "acme.db");
            ctx.IsResolved.Should().BeTrue();
            ctx.TenantId.Should().Be(7);
            ctx.TenantSlug.Should().Be("acme");
            ctx.DatabaseFile.Should().Be("acme.db");
        }

        [Fact]
        public void Resolve_Eh_Idempotente_Primeira_Chamada_Vence()
        {
            var ctx = Create();
            ctx.Resolve(1, "acme", "acme.db");
            ctx.Resolve(2, "demo", "demo.db"); // deveria ser ignorada
            ctx.TenantId.Should().Be(1);
            ctx.TenantSlug.Should().Be("acme");
            ctx.DatabaseFile.Should().Be("acme.db");
        }
    }
}
