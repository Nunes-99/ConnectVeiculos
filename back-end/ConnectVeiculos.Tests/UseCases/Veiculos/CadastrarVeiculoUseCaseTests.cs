using ConnectVeiculos.Application.InputModels.Veiculos;
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
    public class CadastrarVeiculoUseCaseTests
    {
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<INotificacaoService> _notificacaoServiceMock;
        private readonly Mock<ICatalogoHubService> _catalogoHubServiceMock;
        private readonly Mock<IMercadoLivreService> _mercadoLivreServiceMock;
        private readonly Mock<IFacebookCatalogService> _facebookServiceMock;
        private readonly Mock<IGoogleMerchantService> _googleServiceMock;
        private readonly Mock<IVeiculoPublicacaoRepository> _publicacaoRepositoryMock;
        private readonly CadastrarVeiculoUseCase _useCase;

        public CadastrarVeiculoUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _notificacaoServiceMock = new Mock<INotificacaoService>();
            _catalogoHubServiceMock = new Mock<ICatalogoHubService>();
            _mercadoLivreServiceMock = new Mock<IMercadoLivreService>();
            _facebookServiceMock = new Mock<IFacebookCatalogService>();
            _googleServiceMock = new Mock<IGoogleMerchantService>();
            _publicacaoRepositoryMock = new Mock<IVeiculoPublicacaoRepository>();
            _useCase = new CadastrarVeiculoUseCase(
                _veiculoRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _notificacaoServiceMock.Object,
                _catalogoHubServiceMock.Object,
                _mercadoLivreServiceMock.Object,
                _facebookServiceMock.Object,
                _googleServiceMock.Object,
                _publicacaoRepositoryMock.Object,
                NullLogger<CadastrarVeiculoUseCase>.Instance,
                new Mock<IFavoritoNotificacaoService>().Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveCadastrarVeiculo()
        {
            // Arrange
            var input = new VeiculoInputModel
            {
                R_LojId = 1,
                R_CatId = 1,
                VeiMarca = "Toyota",
                VeiModelo = "Corolla",
                VeiAno = 2023,
                VeiPlaca = "ABC1D23",
                VeiChassi = "9BWZZZ377VT004251",
                VeiCor = "Prata",
                VeiKm = 15000,
                VeiPreco = 120000.00m,
                VeiDtEntrada = DateTime.Now,
                VeiSts = "A",
                VeiSitSts = "D",
                VeiPrecoCompra = 100000.00m
            };

            _veiculoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Veiculo>()))
                .ReturnsAsync(1);

            // Act
            var result = await _useCase.Execute(input);

            // Assert
            result.Should().Be(1);
            _veiculoRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Veiculo>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollbackELancarExcecao()
        {
            // Arrange
            var input = new VeiculoInputModel
            {
                R_LojId = 1,
                R_CatId = 1,
                VeiMarca = "Toyota",
                VeiModelo = "Corolla",
                VeiAno = 2023,
                VeiPlaca = "ABC1D23"
            };

            _veiculoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Veiculo>()))
                .ThrowsAsync(new Exception("Erro no banco de dados"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Erro no banco de dados");
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Never);
        }

        [Fact]
        public async Task Execute_DeveCriarVeiculoComDadosCorretos()
        {
            // Arrange
            var input = new VeiculoInputModel
            {
                R_LojId = 1,
                R_CatId = 2,
                VeiMarca = "Honda",
                VeiModelo = "Civic",
                VeiAno = 2024,
                VeiPlaca = "XYZ9A88",
                VeiChassi = "1HGBH41JXMN109186",
                VeiCor = "Preto",
                VeiKm = 0,
                VeiPreco = 150000.00m,
                VeiDtEntrada = new DateTime(2024, 1, 15),
                VeiSts = "A",
                VeiSitSts = "D",
                VeiPrecoCompra = 130000.00m
            };

            Veiculo veiculoCriado = null;
            _veiculoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Veiculo>()))
                .Callback<Veiculo>(v => veiculoCriado = v)
                .ReturnsAsync(1);

            // Act
            await _useCase.Execute(input);

            // Assert
            veiculoCriado.Should().NotBeNull();
            veiculoCriado.VeiMarca.Should().Be("Honda");
            veiculoCriado.VeiModelo.Should().Be("Civic");
            veiculoCriado.VeiAno.Should().Be(2024);
            veiculoCriado.VeiPlaca.Should().Be("XYZ9A88");
            veiculoCriado.VeiPreco.Should().Be(150000.00m);
        }
    }
}
