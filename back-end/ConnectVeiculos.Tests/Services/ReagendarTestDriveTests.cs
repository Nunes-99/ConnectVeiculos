using ConnectVeiculos.API.Controllers;
using ConnectVeiculos.Core.Entities.TestDrives;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Remarcar test drive, que ate 18/09/2026 nao existia: so havia agendar,
    /// listar e trocar o status. Quem precisasse mudar o horario tinha de
    /// cancelar e criar outro — o que perde o agendamento original e manda um
    /// aviso de cancelamento para um cliente que so combinou outro dia.
    /// </summary>
    public class ReagendarTestDriveTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ConnectVeiculosDbContext _context;
        private readonly Mock<ITestDriveNotificacaoService> _notificacao = new();

        public ReagendarTestDriveTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ConnectVeiculosDbContext(options);
            _context.Database.EnsureCreated();

            _notificacao.Setup(n => n.NotificarConfirmacaoAsync(It.IsAny<TestDrive>()))
                .ReturnsAsync(new TestDriveNotificacaoResult { Enviada = true, Motivo = "ok" });
        }

        private int CriarTestDrive(string status, string horario = "10:00")
        {
            var td = new TestDrive(0, 1, 1, "Cliente", "11999999999", "11999999999",
                "", DateTime.Today.AddDays(1), horario, "", status);
            _context.TestDrives.Add(td);
            _context.SaveChanges();
            return td.TdrId;
        }

        private TestDrivesController CriarController() => new(_context);

        [Fact]
        public async Task Remarca_data_e_horario()
        {
            var id = CriarTestDrive("P");
            var novaData = DateTime.Today.AddDays(5);

            var resposta = await CriarController().Reagendar(id,
                new ReagendarTestDriveRequest { DataAgendamento = novaData, Horario = "16:45" },
                _notificacao.Object);

            resposta.Should().BeOfType<OkObjectResult>();

            var td = await _context.TestDrives.FindAsync(id);
            td!.TdrDataAgendamento.Date.Should().Be(novaData.Date);
            td.TdrHorario.Should().Be("16:45");
            td.TdrStatus.Should().Be("P", "remarcar nao muda o status");
        }

        [Fact]
        public async Task Remarcar_confirmado_avisa_o_cliente()
        {
            var id = CriarTestDrive("C");

            await CriarController().Reagendar(id,
                new ReagendarTestDriveRequest { DataAgendamento = DateTime.Today.AddDays(3), Horario = "09:00" },
                _notificacao.Object);

            _notificacao.Verify(n => n.NotificarConfirmacaoAsync(It.IsAny<TestDrive>()), Times.Once,
                "a confirmacao que o cliente tem na mao passou a estar errada");
        }

        [Fact]
        public async Task Remarcar_pendente_nao_avisa_ninguem()
        {
            var id = CriarTestDrive("P");

            await CriarController().Reagendar(id,
                new ReagendarTestDriveRequest { DataAgendamento = DateTime.Today.AddDays(3), Horario = "09:00" },
                _notificacao.Object);

            _notificacao.Verify(n => n.NotificarConfirmacaoAsync(It.IsAny<TestDrive>()), Times.Never,
                "nao havia confirmacao nenhuma para corrigir");
        }

        [Theory]
        [InlineData("R")]
        [InlineData("X")]
        public async Task Realizado_e_cancelado_nao_se_remarcam(string status)
        {
            var id = CriarTestDrive(status);

            var resposta = await CriarController().Reagendar(id,
                new ReagendarTestDriveRequest { DataAgendamento = DateTime.Today.AddDays(3) },
                _notificacao.Object);

            resposta.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Nao_remarca_para_o_passado()
        {
            var id = CriarTestDrive("C");

            var resposta = await CriarController().Reagendar(id,
                new ReagendarTestDriveRequest { DataAgendamento = DateTime.Today.AddDays(-1) },
                _notificacao.Object);

            resposta.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Horario_em_branco_vira_string_vazia_e_nao_nulo()
        {
            var id = CriarTestDrive("P");

            await CriarController().Reagendar(id,
                new ReagendarTestDriveRequest { DataAgendamento = DateTime.Today.AddDays(2), Horario = "   " },
                _notificacao.Object);

            var td = await _context.TestDrives.FindAsync(id);
            td!.TdrHorario.Should().Be("");
        }

        [Fact]
        public async Task Test_drive_inexistente_da_404()
        {
            var resposta = await CriarController().Reagendar(999,
                new ReagendarTestDriveRequest { DataAgendamento = DateTime.Today.AddDays(1) },
                _notificacao.Object);

            resposta.Should().BeOfType<NotFoundResult>();
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
