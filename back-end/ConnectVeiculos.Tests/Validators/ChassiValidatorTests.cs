using ConnectVeiculos.Core.Validators;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Validators
{
    public class ChassiValidatorTests
    {
        // Chassi de 17 caracteres cujo digito verificador NAO fecha pelo padrao
        // norte-americano — situacao normal em veiculo fabricado pro Brasil.
        private const string ChassiBrasileiro = "9BWZZZ377VT004251";

        // Mesmo chassi com o digito da posicao 9 ajustado pra fechar o calculo.
        private const string ChassiComDigitoOk = "9BWZZZ372VT004251";

        [Fact]
        public void IsValid_DeveAceitarChassiBrasileiroSemDigitoVerificadorValido()
        {
            // Era exatamente esse caso que travava o cadastro de veiculo.
            ChassiValidator.IsValid(ChassiBrasileiro).Should().BeTrue();
        }

        [Fact]
        public void IsValid_DeveAceitarChassiComDigitoVerificadorValido()
        {
            ChassiValidator.IsValid(ChassiComDigitoOk).Should().BeTrue();
        }

        [Theory]
        [InlineData("9BWZZZ377VT00425")]    // 16 caracteres
        [InlineData("9BWZZZ377VT0042511")]  // 18 caracteres
        [InlineData("9BWZZZ377VT00425I")]   // contem I
        [InlineData("9BWZZZ377VT00425O")]   // contem O
        [InlineData("9BWZZZ377VT00425Q")]   // contem Q
        [InlineData("9BWZZZ377VT0042-1")]   // caractere especial
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void IsValid_DeveRejeitarFormatoInvalido(string chassi)
        {
            ChassiValidator.IsValid(chassi).Should().BeFalse();
        }

        [Fact]
        public void IsValid_DeveAceitarMinusculasEEspacosAoRedor()
        {
            ChassiValidator.IsValid("  9bwzzz377vt004251  ").Should().BeTrue();
        }

        [Fact]
        public void HasValidCheckDigit_DeveDistinguirOsDoisCasos()
        {
            ChassiValidator.HasValidCheckDigit(ChassiComDigitoOk).Should().BeTrue();
            ChassiValidator.HasValidCheckDigit(ChassiBrasileiro).Should().BeFalse();
        }

        [Fact]
        public void HasValidCheckDigit_ComFormatoInvalido_DeveSerFalse()
        {
            ChassiValidator.HasValidCheckDigit("ABC").Should().BeFalse();
        }

        [Fact]
        public void GetInfo_DeveExtrairDadosDeChassiComFormatoValido()
        {
            var info = ChassiValidator.GetInfo(ChassiBrasileiro);

            info.Should().NotBeNull();
            info.WMI.Should().Be("9BW");
        }
    }
}
