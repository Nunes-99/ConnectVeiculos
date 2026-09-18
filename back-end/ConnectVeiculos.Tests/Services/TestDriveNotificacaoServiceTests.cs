using ConnectVeiculos.Core.Entities.TestDrives;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Services.Notificacao;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Nenhuma variavel do template pode sair em branco: a Meta recusa o
    /// template inteiro quando isso acontece, e a recusa nao diz qual variavel
    /// foi — o erro chega parecendo que o template nao existe ou nao esta
    /// aprovado, que e um problema completamente diferente.
    ///
    /// O horario do test drive e opcional no cadastro (coluna TEXT sem NOT
    /// NULL), e o lembrete agendado para 18/09/2026 estava salvo justamente
    /// sem horario — ou seja, o primeiro envio de verdade ia falhar por isso.
    /// </summary>
    public class TestDriveNotificacaoServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ConnectVeiculosDbContext _context;
        private readonly Mock<IWhatsAppService> _whatsApp = new();
        private List<string> _parametrosEnviados = new();

        public TestDriveNotificacaoServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ConnectVeiculosDbContext(options);
            _context.Database.EnsureCreated();

            _whatsApp.Setup(w => w.IsConfiguredAsync()).ReturnsAsync(true);
            _whatsApp.Setup(w => w.EnviarTemplateAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Callback<string, string, string, IEnumerable<string>>(
                    (_, _, _, p) => _parametrosEnviados = p.ToList())
                .ReturnsAsync(true);
        }

        private TestDriveNotificacaoService CriarServico() =>
            new(_whatsApp.Object, _context, NullLogger<TestDriveNotificacaoService>.Instance);

        private static TestDrive TestDriveSemHorario() =>
            new(1, 99, null, "Vitor Teste Lembrete", "11953179948", "11953179948",
                "", new DateTime(2026, 9, 19), horarioVazio, "", "C");

        private const string horarioVazio = "";

        [Fact]
        public async Task Lembrete_sem_horario_nao_manda_parametro_em_branco()
        {
            var resultado = await CriarServico().NotificarLembreteAsync(TestDriveSemHorario());

            resultado.Enviada.Should().BeTrue();
            _parametrosEnviados.Should().NotBeEmpty();
            _parametrosEnviados.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p),
                "a Meta recusa o template inteiro quando uma variavel vem vazia");
            _parametrosEnviados[2].Should().Be("a combinar");
        }

        [Fact]
        public async Task Lembrete_com_horario_usa_o_horario_informado()
        {
            var td = new TestDrive(1, 99, null, "Vitor", "11953179948", "11953179948",
                "", new DateTime(2026, 9, 19), " 14:30 ", "", "C");

            await CriarServico().NotificarLembreteAsync(td);

            _parametrosEnviados[2].Should().Be("14:30");
        }

        [Fact]
        public async Task Confirmacao_sem_horario_tambem_fica_protegida()
        {
            await CriarServico().NotificarConfirmacaoAsync(TestDriveSemHorario());

            _parametrosEnviados.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p));
        }

        [Fact]
        public async Task Cancelamento_sem_horario_tambem_fica_protegido()
        {
            await CriarServico().NotificarCancelamentoAsync(TestDriveSemHorario());

            _parametrosEnviados.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p));
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
