using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Database.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Auditoria
{
    /// <summary>
    /// Achado na validacao de 04/11/2026: o INSERT de veiculo aparecia no log de
    /// auditoria com o id -2147482646 (id temporario do EF, lido antes de gravar).
    /// </summary>
    public class AuditInterceptorTests : IDisposable
    {
        private readonly SqliteConnection _conexao;
        private readonly ConnectVeiculosDbContext _ctx;

        public AuditInterceptorTests()
        {
            _conexao = new SqliteConnection("DataSource=:memory:");
            _conexao.Open();
            var interceptor = new AuditInterceptor(new Mock<IHttpContextAccessor>().Object);
            _ctx = new ConnectVeiculosDbContext(new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                .UseSqlite(_conexao).AddInterceptors(interceptor).Options);
            _ctx.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conexao.Dispose();
        }

        [Fact]
        public async Task Insert_grava_o_id_definitivo_e_nao_o_temporario()
        {
            var cat = new Categoria(0, "SUV", "", true);
            _ctx.Categorias.Add(cat);
            await _ctx.SaveChangesAsync();

            var log = await _ctx.LogsAuditoria.AsNoTracking().SingleAsync(l => l.LogAcao == "INSERT");
            log.LogRegistroId.Should().Be(cat.CatId).And.BePositive();
            log.LogDadosNovos.Should().Contain($"\"CatId\":{cat.CatId}");
        }

        [Fact]
        public void Insert_sincrono_tambem_grava_o_id_definitivo()
        {
            var cat = new Categoria(0, "Hatch", "", true);
            _ctx.Categorias.Add(cat);
            _ctx.SaveChanges();

            _ctx.LogsAuditoria.AsNoTracking().Single(l => l.LogAcao == "INSERT").LogRegistroId.Should().Be(cat.CatId);
        }

        [Fact]
        public async Task Update_e_delete_continuam_registrados()
        {
            var cat = new Categoria(0, "Sedan", "", true);
            _ctx.Categorias.Add(cat);
            await _ctx.SaveChangesAsync();
            _ctx.Entry(cat).Property(c => c.CatNome).CurrentValue = "Sedã";
            await _ctx.SaveChangesAsync();
            _ctx.Categorias.Remove(cat);
            await _ctx.SaveChangesAsync();

            var acoes = await _ctx.LogsAuditoria.AsNoTracking().Where(l => l.LogRegistroId == cat.CatId).Select(l => l.LogAcao).ToListAsync();
            acoes.Should().BeEquivalentTo(new[] { "INSERT", "UPDATE", "DELETE" });
        }
    }
}
