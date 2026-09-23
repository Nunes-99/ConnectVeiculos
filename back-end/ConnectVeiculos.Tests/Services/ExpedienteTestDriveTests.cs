using ConnectVeiculos.Application.UseCases.Catalogo;
using ConnectVeiculos.Core.Entities.TestDrives;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    public class ExpedienteTestDriveTests
    {
        // Quarta-feira, 23/09/2026, 14:36 em Brasilia — o momento do teste que achou o bug.
        private static readonly DateTime Agora = new(2026, 9, 23, 14, 36, 0);
        private static readonly string[] Nenhum = Array.Empty<string>();

        [Fact]
        public void Domingo_NaoTemHorario()
        {
            ExpedienteTestDrive.HorariosLivres(new DateTime(2026, 9, 27), Nenhum, Agora)
                .Should().BeEmpty();
        }

        [Fact]
        public void Sabado_VaiAteOMeioDia()
        {
            ExpedienteTestDrive.HorariosLivres(new DateTime(2026, 9, 26), Nenhum, Agora)
                .Should().Equal("08:00", "09:00", "10:00", "11:00");
        }

        [Fact]
        public void DiaDeSemana_TemOExpedienteInteiro()
        {
            ExpedienteTestDrive.HorariosLivres(new DateTime(2026, 9, 28), Nenhum, Agora)
                .Should().Equal("08:00", "09:00", "10:00", "11:00", "13:00", "14:00", "15:00", "16:00", "17:00");
        }

        [Fact]
        public void Hoje_SoOfereceHorarioQueAindaNaoPassou()
        {
            ExpedienteTestDrive.HorariosLivres(Agora.Date, Nenhum, Agora)
                .Should().Equal("15:00", "16:00", "17:00");
        }

        [Fact]
        public void HorarioOcupado_SaiDaLista()
        {
            ExpedienteTestDrive.HorariosLivres(new DateTime(2026, 9, 28), new[] { "10:00", null }, Agora)
                .Should().NotContain("10:00").And.Contain("09:00");
        }

        [Theory]
        [InlineData(2026, 9, 27, "10:00", "domingos")]
        [InlineData(2026, 9, 26, "13:00", "meio-dia")]
        [InlineData(2026, 9, 26, "17:00", "meio-dia")]
        [InlineData(2026, 9, 22, "10:00", "passado")]
        [InlineData(2026, 9, 23, "09:00", "disponível")]
        [InlineData(2026, 9, 28, "", "horário")]
        public void Agendamento_ForaDaRegra_EhRecusado(int ano, int mes, int dia, string horario, string trechoDoMotivo)
        {
            ExpedienteTestDrive.MotivoRecusa(new DateTime(ano, mes, dia), horario, Nenhum, Agora)
                .Should().Contain(trechoDoMotivo);
        }

        [Fact]
        public void Agendamento_DentroDaRegra_EhAceito()
        {
            ExpedienteTestDrive.MotivoRecusa(new DateTime(2026, 9, 26), "11:00", Nenhum, Agora)
                .Should().BeNull();
        }

        [Fact]
        public void Agendamento_EmHorarioJaTomado_EhRecusado()
        {
            ExpedienteTestDrive.MotivoRecusa(new DateTime(2026, 9, 28), "10:00", new[] { "10:00" }, Agora)
                .Should().Contain("disponível");
        }

        [Theory]
        [InlineData("TST0A01", "A01")]
        [InlineData("abc-1234", "234")]
        [InlineData("AB", "AB")]
        [InlineData(null, "")]
        public void CatalogoPublico_SoMostraOFinalDaPlaca(string placa, string esperado)
        {
            ConsultarCatalogoUseCase.FinalDaPlaca(placa).Should().Be(esperado);
        }
    }
}
