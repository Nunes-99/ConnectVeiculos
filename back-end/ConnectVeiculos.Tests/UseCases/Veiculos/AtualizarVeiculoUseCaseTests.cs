using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.UseCases.Veiculos;
using ConnectVeiculos.Core.Entities.Publicacoes;
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
    public class AtualizarVeiculoUseCaseTests
    {
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<INotificacaoService> _notificacaoServiceMock;
        private readonly Mock<ICatalogoHubService> _catalogoHubServiceMock;
        private readonly Mock<IMercadoLivreService> _mercadoLivreServiceMock;
        private readonly Mock<IFacebookCatalogService> _facebookServiceMock;
        private readonly Mock<IFacebookPagePostService> _facebookPagePostServiceMock;
        private readonly Mock<IGoogleMerchantService> _googleServiceMock;
        private readonly Mock<IVeiculoPublicacaoRepository> _publicacaoRepositoryMock;
        private readonly AtualizarVeiculoUseCase _useCase;

        public AtualizarVeiculoUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _notificacaoServiceMock = new Mock<INotificacaoService>();
            _catalogoHubServiceMock = new Mock<ICatalogoHubService>();
            _mercadoLivreServiceMock = new Mock<IMercadoLivreService>();
            _facebookServiceMock = new Mock<IFacebookCatalogService>();
            _facebookPagePostServiceMock = new Mock<IFacebookPagePostService>();
            _googleServiceMock = new Mock<IGoogleMerchantService>();
            _publicacaoRepositoryMock = new Mock<IVeiculoPublicacaoRepository>();
            _useCase = new AtualizarVeiculoUseCase(
                _veiculoRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _notificacaoServiceMock.Object,
                _catalogoHubServiceMock.Object,
                _mercadoLivreServiceMock.Object,
                _facebookServiceMock.Object,
                _facebookPagePostServiceMock.Object,
                _googleServiceMock.Object,
                _publicacaoRepositoryMock.Object,
                NullLogger<AtualizarVeiculoUseCase>.Instance,
                 new Mock<IFavoritoNotificacaoService>().Object,
                 new Mock<ITenantContext>().Object,
                 new Mock<IIndexNowService>().Object,
                 new Mock<ITenantBackgroundRunner>().Object);
        }

        /// <summary>
        /// Quando o carro e vendido, o post que ficou no Facebook continua
        /// anunciando um veiculo que nao existe mais. A legenda e reescrita com o
        /// selo em vez de o post ser apagado — apagar levaria junto os
        /// comentarios e o alcance ja conquistados.
        /// </summary>
        [Theory]
        [InlineData("V")]
        [InlineData("R")]
        public async Task Execute_QuandoVeiculoSaiDeDisponivel_DeveMarcarOPostDoFacebook(string novoStatus)
        {
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(VeiculoDisponivel());
            _publicacaoRepositoryMock
                .Setup(x => x.GetAtivaByVeiculoEPlataformaAsync(1, "FacebookPage"))
                .ReturnsAsync(new VeiculoPublicacao(1, "FacebookPage", "1175_122110", "https://fb/p"));

            await _useCase.Execute(InputComStatus(novoStatus));

            _facebookPagePostServiceMock.Verify(
                x => x.MarcarPostComoIndisponivelAsync("1175_122110", 1, novoStatus), Times.Once);
        }

        [Fact]
        public async Task Execute_QuandoVeiculoContinuaDisponivel_NaoDeveMarcarNada()
        {
            // Corrigir o preco de um carro a venda nao pode carimbar "VENDIDO" no post.
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(VeiculoDisponivel());

            await _useCase.Execute(InputComStatus("D"));

            _facebookPagePostServiceMock.Verify(
                x => x.MarcarPostComoIndisponivelAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Execute_SemPostNoFacebook_NaoDeveChamarAMeta()
        {
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(VeiculoDisponivel());
            _publicacaoRepositoryMock
                .Setup(x => x.GetAtivaByVeiculoEPlataformaAsync(1, "FacebookPage"))
                .ReturnsAsync((VeiculoPublicacao)null);

            await _useCase.Execute(InputComStatus("V"));

            _facebookPagePostServiceMock.Verify(
                x => x.MarcarPostComoIndisponivelAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Execute_QuandoAMetaFalha_NaoDevePerderAAtualizacaoDoVeiculo()
        {
            // Token da Page expirado nao pode impedir o operador de marcar o carro
            // como vendido no sistema.
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(VeiculoDisponivel());
            _publicacaoRepositoryMock
                .Setup(x => x.GetAtivaByVeiculoEPlataformaAsync(1, "FacebookPage"))
                .ReturnsAsync(new VeiculoPublicacao(1, "FacebookPage", "1175_122110", "https://fb/p"));
            _facebookPagePostServiceMock
                .Setup(x => x.MarcarPostComoIndisponivelAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("token expirado"));

            var acao = async () => await _useCase.Execute(InputComStatus("V"));

            await acao.Should().NotThrowAsync();
            _veiculoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Veiculo>()), Times.Once);
        }

        private static Veiculo VeiculoDisponivel() => new(
            1, 1, 1, "Toyota", "Corolla", 2024, "ABC1D23", "9BWZZZ377VT004251",
            "Branco", 10000, 145000m, DateTime.Now, "D", "D", 130000m);

        private static VeiculoInputModel InputComStatus(string status) => new()
        {
            VeiId = 1,
            R_LojId = 1,
            R_CatId = 1,
            VeiMarca = "Toyota",
            VeiModelo = "Corolla",
            VeiAno = 2024,
            VeiPlaca = "ABC1D23",
            VeiPreco = 145000m,
            VeiDtEntrada = DateTime.Now,
            VeiSts = status,
            VeiSitSts = "D"
        };

        [Fact]
        public async Task Execute_ComDadosValidos_DeveAtualizarVeiculo()
        {
            // Arrange
            var veiculoExistente = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023,
                "ABC1D23", "9BWZZZ377VT004251", "Prata", 15000, 120000.00m,
                DateTime.Now, "A", "D", 100000.00m);

            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(veiculoExistente);

            var input = new VeiculoInputModel
            {
                VeiId = 1,
                R_LojId = 1,
                R_CatId = 1,
                VeiMarca = "Toyota",
                VeiModelo = "Corolla XEi",
                VeiAno = 2023,
                VeiPlaca = "ABC1D23",
                VeiChassi = "9BWZZZ377VT004251",
                VeiCor = "Preto",
                VeiKm = 20000,
                VeiPreco = 115000.00m,
                VeiDtEntrada = DateTime.Now,
                VeiSts = "A",
                VeiSitSts = "D",
                VeiPrecoCompra = 100000.00m
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            _veiculoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Veiculo>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComVeiculoInexistente_DeveLancarExcecao()
        {
            // Arrange
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Veiculo)null);

            var input = new VeiculoInputModel { VeiId = 999 };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Veículo não encontrado.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var veiculoExistente = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023,
                "ABC1D23", "9BWZZZ377VT004251", "Prata", 15000, 120000.00m,
                DateTime.Now, "A", "D", 100000.00m);

            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(veiculoExistente);

            _veiculoRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Veiculo>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            var input = new VeiculoInputModel
            {
                VeiId = 1,
                R_LojId = 1,
                R_CatId = 1,
                VeiMarca = "Toyota",
                VeiModelo = "Corolla"
            };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
