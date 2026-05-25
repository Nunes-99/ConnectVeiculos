using ConnectVeiculos.Application.InputModels.Vendas;
using ConnectVeiculos.Application.UseCases.Vendas;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Vendas
{
    public class RegistrarVendaUseCaseTests
    {
        private readonly Mock<IVendaRepository> _vendaRepositoryMock;
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<INotificacaoService> _notificacaoServiceMock;
        private readonly Mock<ICatalogoHubService> _catalogoHubServiceMock;
        private readonly RegistrarVendaUseCase _useCase;

        public RegistrarVendaUseCaseTests()
        {
            _vendaRepositoryMock = new Mock<IVendaRepository>();
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _notificacaoServiceMock = new Mock<INotificacaoService>();
            _catalogoHubServiceMock = new Mock<ICatalogoHubService>();
             var tenantContextMock = new Mock<ITenantContext>();
            _useCase = new RegistrarVendaUseCase(
                _vendaRepositoryMock.Object,
                _veiculoRepositoryMock.Object,
                _emailServiceMock.Object,
                _notificacaoServiceMock.Object,
                 _catalogoHubServiceMock.Object,
                 tenantContextMock.Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveRegistrarVenda()
        {
            // Arrange
            var veiculo = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023,
                "ABC1D23", "9BWZZZ377VT004251", "Prata", 15000, 120000.00m,
                DateTime.Now, "A", "D", 100000.00m);

            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(veiculo);

            _vendaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Venda>()))
                .ReturnsAsync(1);

            var input = new VendaInputModel
            {
                R_VeiId = 1,
                R_UsuId = 1,
                VenDtVenda = DateTime.Now,
                VenValor = 118000.00m,
                VenComissaoPorc = 3,
                VenCompradorNome = "Maria Silva",
                VenCompradorCpf = "12345678901",
                VenCompradorTelefone = "11999999999",
                VenCompradorEmail = "maria@email.com",
                VenCompradorEndereco = "Rua das Flores, 123",
                VenFormaPagamento = "Financiamento",
                VenObservacao = "Cliente preferencial"
            };

            // Act
            var result = await _useCase.Execute(input);

            // Assert
            result.Should().Be(1);
            _vendaRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Venda>()), Times.Once);
            _veiculoRepositoryMock.Verify(x => x.UpdateAsync(It.Is<Veiculo>(v => v.VeiSts == "V")), Times.Once);
        }

        [Fact]
        public async Task Execute_ComVeiculoInexistente_DeveLancarExcecao()
        {
            // Arrange
            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Veiculo)null);

            var input = new VendaInputModel { R_VeiId = 999 };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<DomainException>().WithMessage("Veículo não encontrado.");
        }

        [Fact]
        public async Task Execute_ComVeiculoJaVendido_DeveLancarExcecao()
        {
            // Arrange
            var veiculo = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023,
                "ABC1D23", "9BWZZZ377VT004251", "Prata", 15000, 120000.00m,
                DateTime.Now, "V", "V", 100000.00m); // Status "V" = Vendido

            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(veiculo);

            var input = new VendaInputModel { R_VeiId = 1 };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<DomainException>().WithMessage("Este veículo já foi vendido.");
        }

        [Fact]
        public async Task Execute_ComEmailComprador_DeveEnviarEmail()
        {
            // Arrange
            var veiculo = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023,
                "ABC1D23", "9BWZZZ377VT004251", "Prata", 15000, 120000.00m,
                DateTime.Now, "A", "D", 100000.00m);

            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(veiculo);

            _vendaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Venda>()))
                .ReturnsAsync(1);

            var input = new VendaInputModel
            {
                R_VeiId = 1,
                R_UsuId = 1,
                VenDtVenda = DateTime.Now,
                VenValor = 118000.00m,
                VenComissaoPorc = 3,
                VenCompradorNome = "Maria Silva",
                VenCompradorEmail = "maria@email.com"
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            _emailServiceMock.Verify(x => x.SendVendaConfirmadaAsync(
                "maria@email.com",
                "Maria Silva",
                It.IsAny<string>(),
                118000.00m), Times.Once);
        }

        [Fact]
        public async Task Execute_DeveCalcularComissaoCorretamente()
        {
            // Arrange
            var veiculo = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023,
                "ABC1D23", "9BWZZZ377VT004251", "Prata", 15000, 120000.00m,
                DateTime.Now, "A", "D", 100000.00m);

            _veiculoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(veiculo);

            Venda vendaCriada = null;
            _vendaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Venda>()))
                .Callback<Venda>(v => vendaCriada = v)
                .ReturnsAsync(1);

            var input = new VendaInputModel
            {
                R_VeiId = 1,
                R_UsuId = 1,
                VenDtVenda = DateTime.Now,
                VenValor = 100000.00m,
                VenComissaoPorc = 5,
                VenCompradorNome = "João"
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            vendaCriada.Should().NotBeNull();
            vendaCriada.VenComissaoValor.Should().Be(5000.00m); // 5% de 100000
        }
    }
}
