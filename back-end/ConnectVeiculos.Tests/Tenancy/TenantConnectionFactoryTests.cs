using ConnectVeiculos.Core.Interfaces.Tenancy;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Tenancy
{
    public class TenantConnectionFactoryTests
    {
        // Reflection helper — TenantConnectionFactory eh internal
        private static ITenantConnectionFactory Create(ITenantContext tenant, IConfiguration config)
        {
            var infraAssembly = typeof(ConnectVeiculos.Infrastructure.IoC.DependencyInjectionExtensions).Assembly;
            var type = infraAssembly.GetType("ConnectVeiculos.Infrastructure.Tenancy.TenantConnectionFactory", throwOnError: true)!;
            return (ITenantConnectionFactory)Activator.CreateInstance(type, tenant, config)!;
        }

        private static IConfiguration BuildConfig(Dictionary<string, string?>? values = null)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
                .Build();
        }

        [Fact]
        public void Sem_Tenant_Resolvido_Cai_No_Fallback_Default_Connection()
        {
            var tenantMock = new Mock<ITenantContext>();
            tenantMock.SetupGet(t => t.IsResolved).Returns(false);

            var config = BuildConfig(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=fallback.db"
            });

            var factory = Create(tenantMock.Object, config);
            factory.GetConnectionString().Should().Be("Data Source=fallback.db");
        }

        [Fact]
        public void Tenant_Resolvido_Usa_Database_File_Do_Tenant()
        {
            var tenantMock = new Mock<ITenantContext>();
            tenantMock.SetupGet(t => t.IsResolved).Returns(true);
            tenantMock.SetupGet(t => t.DatabaseFile).Returns("acme.db");

            // Reseta TENANTS_DATA_DIR pra valor previsivel
            Environment.SetEnvironmentVariable("TENANTS_DATA_DIR", "/test/data");
            try
            {
                var factory = Create(tenantMock.Object, BuildConfig());
                var conn = factory.GetConnectionString();
                conn.Should().StartWith("Data Source=");
                conn.Should().Contain("acme.db");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TENANTS_DATA_DIR", null);
            }
        }

        [Fact]
        public void GetConnectionStringForTenant_Monta_Path_Independente_Do_Contexto()
        {
            var tenantMock = new Mock<ITenantContext>();
            tenantMock.SetupGet(t => t.IsResolved).Returns(false); // contexto nao resolvido

            Environment.SetEnvironmentVariable("TENANTS_DATA_DIR", "/test/data");
            try
            {
                var factory = Create(tenantMock.Object, BuildConfig());
                var conn = factory.GetConnectionStringForTenant("xpto", "xpto.db");
                conn.Should().StartWith("Data Source=");
                conn.Should().Contain("xpto.db");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TENANTS_DATA_DIR", null);
            }
        }
    }
}
