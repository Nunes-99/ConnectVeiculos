using ConnectVeiculos.Core.Entities.Tenants;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Tenancy
{
    public class TenantTests
    {
        [Fact]
        public void Construtor_Define_Slug_Nome_E_Default_Database_File()
        {
            var t = new Tenant("acme", "Acme Veiculos");

            t.TenSlug.Should().Be("acme");
            t.TenNome.Should().Be("Acme Veiculos");
            t.TenDatabaseFile.Should().Be("acme.db");
            t.TenStatus.Should().Be(TenantStatus.Active);
            t.TenDtCriacao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Construtor_Aceita_Database_File_Custom()
        {
            var t = new Tenant("default", "Tenant Padrao", "cliente.db");
            t.TenDatabaseFile.Should().Be("cliente.db");
        }

        [Fact]
        public void Construtor_Trata_Database_File_Vazio_Como_Default()
        {
            var t = new Tenant("acme", "Acme", "");
            t.TenDatabaseFile.Should().Be("acme.db");
        }

        [Fact]
        public void Suspender_Muda_Status_Para_Suspended()
        {
            var t = new Tenant("acme", "Acme");
            t.Suspender();
            t.TenStatus.Should().Be(TenantStatus.Suspended);
        }

        [Fact]
        public void Reativar_Volta_Status_Para_Active()
        {
            var t = new Tenant("acme", "Acme");
            t.Suspender();
            t.Reativar();
            t.TenStatus.Should().Be(TenantStatus.Active);
        }

        [Fact]
        public void Renomear_Atualiza_Nome()
        {
            var t = new Tenant("acme", "Acme Velho");
            t.Renomear("Acme Novo");
            t.TenNome.Should().Be("Acme Novo");
        }
    }
}
