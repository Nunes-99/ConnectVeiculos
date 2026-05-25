using ConnectVeiculos.Application.UseCases.Vendas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Email;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases
{
    public class EstornarVendaUseCaseTests
    {
        private readonly Mock<IVendaRepository> _vendaRepositoryMock;
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly EstornarVendaUseCase _useCase;

        public EstornarVendaUseCaseTests()
        {
            _vendaRepositoryMock = new Mock<IVendaRepository>();
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _emailServiceMock.Setup(x => x.SendVendaEstornadaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _useCase = new EstornarVendaUseCase(
                _vendaRepositoryMock.Object,
                _veiculoRepositoryMock.Object,
                _emailServiceMock.Object);
        }

        [Fact]
        public async Task Estornar_Venda_Existente_Deve_Funcionar()
        {
            // Arrange
            var venda = new Venda(1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023,
                "9BWZZZ377VT004251", 85000m, 5m, 4250m, "Joao Silva");

            var veiculo = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234",
                "9BWZZZ377VT004251", "Prata", 15000, 85000m, DateTime.Now, "V", "A", 75000m);

            _vendaRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(venda);
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(veiculo);

            // Act
            await _useCase.Execute(1);

            // Assert
            _vendaRepositoryMock.Verify(x => x.UpdateAsync(It.Is<Venda>(v => v.VenStatus == "E")), Times.Once);
            _veiculoRepositoryMock.Verify(x => x.UpdateAsync(It.Is<Veiculo>(v => v.VeiSts == "D")), Times.Once);
        }

        [Fact]
        public async Task Estornar_Venda_Inexistente_Deve_Lancar_Excecao()
        {
            // Arrange
            _vendaRepositoryMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Venda)null);

            // Act
            Func<Task> act = async () => await _useCase.Execute(999);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("*não encontrada*");
        }

        [Fact]
        public async Task Estornar_Venda_Ja_Estornada_Deve_Lancar_Excecao()
        {
            // Arrange
            var venda = new Venda(1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023,
                "9BWZZZ377VT004251", 85000m, 5m, 4250m, "Joao Silva");
            venda.Estornar(); // Estorna a venda antes

            _vendaRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(venda);

            // Act
            Func<Task> act = async () => await _useCase.Execute(1);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("*ja foi estornada*");
        }
    }
}
