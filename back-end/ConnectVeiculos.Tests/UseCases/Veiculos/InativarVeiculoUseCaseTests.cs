using ConnectVeiculos.Application.UseCases.Veiculos;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Veiculos
{
    public class InativarVeiculoUseCaseTests
    {
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ICatalogoHubService> _catalogoHubServiceMock;
        private readonly Mock<IPublicacaoAutomaticaService> _publicacaoAutomaticaServiceMock;
        private readonly InativarVeiculoUseCase _useCase;

        public InativarVeiculoUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _catalogoHubServiceMock = new Mock<ICatalogoHubService>();
            _publicacaoAutomaticaServiceMock = new Mock<IPublicacaoAutomaticaService>();
             var tenantContextMock = new Mock<ITenantContext>();
            _useCase = new InativarVeiculoUseCase(
                _veiculoRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _catalogoHubServiceMock.Object,
                _publicacaoAutomaticaServiceMock.Object,
                 tenantContextMock.Object,
                NullLogger<InativarVeiculoUseCase>.Instance,
                 new Mock<IIndexNowService>().Object,
                 new Mock<ITenantBackgroundRunner>().Object);
        }

        /// <summary>
        /// Inativar encerrava o anuncio do Mercado Livre e limpava os catalogos,
        /// mas deixava o post da Page no ar anunciando um carro apagado. A rotina
        /// e a mesma de vender ou reservar, entao passou a ser compartilhada.
        /// </summary>
        [Fact]
        public async Task Execute_AoInativar_DeveTirarOVeiculoDasPlataformas()
        {
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(
                new Veiculo(5, 1, 1, "VW", "Gol", 2021, "TST0D04", "9BWZZZ377VT004251",
                            "Branco", 40000, 52000m, DateTime.Now, "D", "D", 45000m));

            await _useCase.Execute(5);

            _publicacaoAutomaticaServiceMock.Verify(
                x => x.MarcarVeiculoIndisponivelAsync(5, "I"), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveInativarVeiculo()
        {
            // Arrange
            var veiculo = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023,
                "ABC1D23", "9BWZZZ377VT004251", "Prata", 15000, 120000.00m,
                DateTime.Now, "A", "D", 100000.00m);

            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(veiculo);

            Veiculo veiculoAtualizado = null;
            _veiculoRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Veiculo>()))
                .Callback<Veiculo>(v => veiculoAtualizado = v)
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.Execute(1);

            // Assert
            veiculoAtualizado.Should().NotBeNull();
            veiculoAtualizado.VeiSts.Should().Be("I");
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            // Arrange
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Veiculo)null);

            // Act
            Func<Task> act = async () => await _useCase.Execute(999);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Veículo não encontrado.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var veiculo = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023,
                "ABC1D23", "9BWZZZ377VT004251", "Prata", 15000, 120000.00m,
                DateTime.Now, "A", "D", 100000.00m);

            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(veiculo);

            _veiculoRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Veiculo>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(1);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
