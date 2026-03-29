using ConnectVeiculos.Infrastructure.Services.Financiamento;
using FluentAssertions;

namespace ConnectVeiculos.Tests.Services
{
    public class FinanciamentoServiceTests
    {
        private readonly FinanciamentoService _service;

        public FinanciamentoServiceTests()
        {
            _service = new FinanciamentoService();
        }

        [Fact]
        public void CalcularPrice_DeveRetornarSimulacaoCorreta()
        {
            // Arrange
            decimal valorVeiculo = 50000;
            decimal entrada = 10000;
            int parcelas = 12;
            decimal taxaAnual = 12; // 12% ao ano

            // Act
            var resultado = _service.CalcularPrice(valorVeiculo, entrada, parcelas, taxaAnual);

            // Assert
            resultado.Should().NotBeNull();
            resultado.Tipo.Should().Be("PRICE");
            resultado.ValorVeiculo.Should().Be(valorVeiculo);
            resultado.ValorEntrada.Should().Be(entrada);
            resultado.ValorFinanciado.Should().Be(40000);
            resultado.NumeroParcelas.Should().Be(parcelas);
            resultado.Parcelas.Should().HaveCount(parcelas);
            resultado.ValorTotalPago.Should().BeGreaterThan(valorVeiculo);

            // Todas as parcelas devem ter o mesmo valor (caracteristica do PRICE)
            var valorParcela = resultado.Parcelas.First().ValorParcela;
            resultado.Parcelas.All(p => p.ValorParcela == valorParcela).Should().BeTrue();
        }

        [Fact]
        public void CalcularSAC_DeveRetornarSimulacaoCorreta()
        {
            // Arrange
            decimal valorVeiculo = 50000;
            decimal entrada = 10000;
            int parcelas = 12;
            decimal taxaAnual = 12;

            // Act
            var resultado = _service.CalcularSAC(valorVeiculo, entrada, parcelas, taxaAnual);

            // Assert
            resultado.Should().NotBeNull();
            resultado.Tipo.Should().Be("SAC");
            resultado.ValorVeiculo.Should().Be(valorVeiculo);
            resultado.ValorFinanciado.Should().Be(40000);
            resultado.Parcelas.Should().HaveCount(parcelas);

            // Amortizacao deve ser constante (caracteristica do SAC)
            var amortizacao = resultado.Parcelas.First().ValorAmortizacao;
            resultado.Parcelas.All(p => Math.Abs(p.ValorAmortizacao - amortizacao) < 0.01m).Should().BeTrue();

            // Parcelas devem diminuir ao longo do tempo (caracteristica do SAC)
            for (int i = 1; i < resultado.Parcelas.Count; i++)
            {
                resultado.Parcelas[i].ValorParcela.Should().BeLessThanOrEqualTo(resultado.Parcelas[i - 1].ValorParcela);
            }
        }

        [Fact]
        public void CalcularPrice_SaldoDevedor_DeveZerarNoFinal()
        {
            // Arrange
            decimal valorVeiculo = 30000;
            decimal entrada = 5000;
            int parcelas = 24;
            decimal taxaAnual = 15;

            // Act
            var resultado = _service.CalcularPrice(valorVeiculo, entrada, parcelas, taxaAnual);

            // Assert - aceita margem de erro por arredondamento
            Math.Abs(resultado.Parcelas.Last().SaldoDevedor).Should().BeLessThan(1);
        }

        [Fact]
        public void CalcularSAC_SaldoDevedor_DeveZerarNoFinal()
        {
            // Arrange
            decimal valorVeiculo = 30000;
            decimal entrada = 5000;
            int parcelas = 24;
            decimal taxaAnual = 15;

            // Act
            var resultado = _service.CalcularSAC(valorVeiculo, entrada, parcelas, taxaAnual);

            // Assert - aceita margem de erro por arredondamento
            Math.Abs(resultado.Parcelas.Last().SaldoDevedor).Should().BeLessThan(1);
        }

        [Fact]
        public void CalcularPrice_JurosTotais_DevemSerPositivos()
        {
            // Arrange
            decimal valorVeiculo = 100000;
            decimal entrada = 20000;
            int parcelas = 48;
            decimal taxaAnual = 18;

            // Act
            var resultado = _service.CalcularPrice(valorVeiculo, entrada, parcelas, taxaAnual);

            // Assert
            resultado.TotalJuros.Should().BePositive();
            resultado.TotalJuros.Should().BeGreaterThan(0);
        }
    }
}
