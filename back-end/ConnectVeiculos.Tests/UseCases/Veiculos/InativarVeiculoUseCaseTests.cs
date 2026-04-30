using ConnectVeiculos.Application.UseCases.Veiculos;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
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
        private readonly Mock<IMercadoLivreService> _mercadoLivreServiceMock;
        private readonly Mock<IFacebookCatalogService> _facebookServiceMock;
        private readonly Mock<IGoogleMerchantService> _googleServiceMock;
        private readonly Mock<IVeiculoPublicacaoRepository> _publicacaoRepositoryMock;
        private readonly InativarVeiculoUseCase _useCase;

        public InativarVeiculoUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _catalogoHubServiceMock = new Mock<ICatalogoHubService>();
            _mercadoLivreServiceMock = new Mock<IMercadoLivreService>();
            _facebookServiceMock = new Mock<IFacebookCatalogService>();
            _googleServiceMock = new Mock<IGoogleMerchantService>();
            _publicacaoRepositoryMock = new Mock<IVeiculoPublicacaoRepository>();
            _useCase = new InativarVeiculoUseCase(
                _veiculoRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _catalogoHubServiceMock.Object,
                _mercadoLivreServiceMock.Object,
                _facebookServiceMock.Object,
                _googleServiceMock.Object,
                _publicacaoRepositoryMock.Object,
                NullLogger<InativarVeiculoUseCase>.Instance);
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
            await act.Should().ThrowAsync<Exception>().WithMessage("Veiculo nao encontrado.");
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
