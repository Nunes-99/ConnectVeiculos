using ConnectVeiculos.Core.Validators;
using FluentAssertions;

namespace ConnectVeiculos.Tests.Validators
{
    public class CnpjValidatorTests
    {
        [Theory]
        [InlineData("11.222.333/0001-81", true)]  // CNPJ valido formatado
        [InlineData("11222333000181", true)]       // CNPJ valido sem formatacao
        [InlineData("11.444.777/0001-61", true)]  // Outro CNPJ valido
        [InlineData("11.111.111/1111-11", false)] // Todos digitos iguais
        [InlineData("00.000.000/0000-00", false)] // Todos zeros
        [InlineData("11.222.333/0001-82", false)] // Digito verificador errado
        [InlineData("12345678901234", false)]      // CNPJ invalido
        [InlineData("1234567890123", false)]       // Menos de 14 digitos
        [InlineData("123456789012345", false)]     // Mais de 14 digitos
        [InlineData("", false)]                    // Vazio
        [InlineData(null, false)]                  // Nulo
        public void IsValid_DeveRetornarResultadoCorreto(string cnpj, bool esperado)
        {
            // Act
            var resultado = CnpjValidator.IsValid(cnpj);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("11222333000181", "11.222.333/0001-81")]
        [InlineData("11.222.333/0001-81", "11.222.333/0001-81")]
        public void Format_DeveFormatarCnpjCorretamente(string cnpj, string esperado)
        {
            // Act
            var resultado = CnpjValidator.Format(cnpj);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("11.222.333/0001-81", "11222333000181")]
        [InlineData("11222333000181", "11222333000181")]
        public void Unformat_DeveRemoverFormatacao(string cnpj, string esperado)
        {
            // Act
            var resultado = CnpjValidator.Unformat(cnpj);

            // Assert
            resultado.Should().Be(esperado);
        }
    }
}
