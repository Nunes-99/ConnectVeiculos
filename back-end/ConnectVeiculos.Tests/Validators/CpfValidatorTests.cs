using ConnectVeiculos.Core.Validators;
using FluentAssertions;

namespace ConnectVeiculos.Tests.Validators
{
    public class CpfValidatorTests
    {
        [Theory]
        [InlineData("529.982.247-25", true)] // CPF valido formatado
        [InlineData("52998224725", true)]    // CPF valido sem formatacao
        [InlineData("529 982 247 25", true)] // CPF valido com espacos
        [InlineData("111.111.111-11", false)] // Todos digitos iguais
        [InlineData("000.000.000-00", false)] // Todos zeros
        [InlineData("123.456.789-00", false)] // CPF invalido
        [InlineData("529.982.247-24", false)] // Digito verificador errado
        [InlineData("12345678901", false)]    // CPF invalido
        [InlineData("1234567890", false)]     // Menos de 11 digitos
        [InlineData("123456789012", false)]   // Mais de 11 digitos
        [InlineData("", false)]               // Vazio
        [InlineData(null, false)]             // Nulo
        public void IsValid_DeveRetornarResultadoCorreto(string cpf, bool esperado)
        {
            // Act
            var resultado = CpfValidator.IsValid(cpf);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("52998224725", "529.982.247-25")]
        [InlineData("529.982.247-25", "529.982.247-25")]
        public void Format_DeveFormatarCpfCorretamente(string cpf, string esperado)
        {
            // Act
            var resultado = CpfValidator.Format(cpf);

            // Assert
            resultado.Should().Be(esperado);
        }

        [Theory]
        [InlineData("529.982.247-25", "52998224725")]
        [InlineData("52998224725", "52998224725")]
        public void Unformat_DeveRemoverFormatacao(string cpf, string esperado)
        {
            // Act
            var resultado = CpfValidator.Unformat(cpf);

            // Assert
            resultado.Should().Be(esperado);
        }
    }
}
