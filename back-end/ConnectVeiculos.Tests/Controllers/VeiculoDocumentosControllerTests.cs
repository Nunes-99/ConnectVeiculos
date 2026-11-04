using ConnectVeiculos.API.Controllers;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ConnectVeiculos.Tests.Controllers
{
    /// <summary>
    /// A tela de Documentos oferece tipos e situacoes que a API recusava
    /// (Laudo Cautelar, Vistoria, Licenciamento, Em andamento).
    /// </summary>
    public class VeiculoDocumentosControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ConnectVeiculosDbContext _db;
        private readonly VeiculoDocumentosController _controller;

        public VeiculoDocumentosControllerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _db = new ConnectVeiculosDbContext(new DbContextOptionsBuilder<ConnectVeiculosDbContext>().UseSqlite(_connection).Options);
            _db.Database.EnsureCreated();
            _db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
            _controller = new VeiculoDocumentosController(_db);
        }

        // Os mesmos codigos de TIPOS_DOCUMENTO e STATUS_DOCUMENTO (veiculo-documento.service.ts).
        [Theory]
        [InlineData("CRLV", "PENDENTE")]
        [InlineData("LAUDO_CAUTELAR", "EM_ANDAMENTO")]
        [InlineData("TRANSFERENCIA", "CONCLUIDO")]
        [InlineData("VISTORIA", "PENDENTE")]
        [InlineData("IPVA", "PENDENTE")]
        [InlineData("LICENCIAMENTO", "EM_ANDAMENTO")]
        [InlineData("SEGURO", "PENDENTE")]
        [InlineData("MULTA", "PENDENTE")]
        [InlineData("FINANCIAMENTO", "PENDENTE")]
        [InlineData("OUTROS", "CONCLUIDO")]
        public async Task Aceita_Os_Tipos_E_Situacoes_Da_Tela(string tipo, string status)
        {
            var r = await _controller.Criar(new CriarDocumentoRequest { VeiculoId = 1, Tipo = tipo, Status = status, DataVencimento = DateTime.Today.AddDays(10) });

            r.Should().BeOfType<OkObjectResult>();
            _db.VeiculosDocumentos.Single().DocTipo.Should().Be(tipo);
        }

        [Fact]
        public async Task Recusa_Tipo_Desconhecido_Com_Mensagem_Em_Json()
        {
            var r = await _controller.Criar(new CriarDocumentoRequest { VeiculoId = 1, Tipo = "XPTO" });

            var bad = r.Should().BeOfType<BadRequestObjectResult>().Subject;
            bad.Value!.GetType().GetProperty("error")!.GetValue(bad.Value).Should().BeOfType<string>()
                .Which.Should().Contain("Tipo inválido");
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }
    }
}
