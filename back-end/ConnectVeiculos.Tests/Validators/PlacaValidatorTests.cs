using ConnectVeiculos.Core.Validators;
using FluentAssertions;

namespace ConnectVeiculos.Tests.Validators
{
    public class PlacaValidatorTests
    {
        [Theory]
        [InlineData("ABC-1234", true)]  // Placa antiga com hifen
        [InlineData("ABC1234", true)]   // Placa antiga sem hifen
        [InlineData("ABC1D23", true)]   // Placa Mercosul
        [InlineData("BRA2E19", true)]   // Placa Mercosul
        [InlineData("RIO2A18", true)]   // Placa Mercosul
        [InlineData("abc-1234", true)]  // Minusculas (case insensitive)
        [InlineData("AB12345", false)]  // Formato invalido
        [InlineData("ABCD123", false)]  // 4 letras
        [InlineData("AB-12345", false)] // 5 numeros
        [InlineData("", false)]         // Vazio
        [InlineData(null, false)]       // Nulo
        public void IsValid_DeveRetornarResultadoCorreto(string placa, bool esperado)
        {
            // Act
            var resultado = PlacaValidator.IsValid(placa);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("ABC1234", PlacaTipo.Antiga)]
        [InlineData("ABC-1234", PlacaTipo.Antiga)]
        [InlineData("ABC1D23", PlacaTipo.Mercosul)]
        [InlineData("BRA2E19", PlacaTipo.Mercosul)]
        [InlineData("INVALID", PlacaTipo.Invalida)]
        public void GetTipo_DeveRetornarTipoCorreto(string placa, PlacaTipo esperado)
        {
            // Act
            var resultado = PlacaValidator.GetTipo(placa);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("ABC1234", "ABC-1234")]   // Formata placa antiga
        [InlineData("ABC-1234", "ABC-1234")]  // Mantem formatacao
        [InlineData("ABC1D23", "ABC1D23")]    // Mercosul nao usa hifen
        public void Format_DeveFormatarPlacaCorretamente(string placa, string esperado)
        {
            // Act
            var resultado = PlacaValidator.Format(placa);

            // Assert
            resultado.Should().Be(esperado);
        }
    }
}
