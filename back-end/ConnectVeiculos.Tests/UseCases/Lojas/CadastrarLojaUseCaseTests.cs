using ConnectVeiculos.Application.InputModels.Lojas;
using ConnectVeiculos.Application.UseCases.Lojas;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Lojas
{
    public class CadastrarLojaUseCaseTests
    {
        private readonly Mock<ILojaRepository> _lojaRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly CadastrarLojaUseCase _useCase;

        public CadastrarLojaUseCaseTests()
        {
            _lojaRepositoryMock = new Mock<ILojaRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new CadastrarLojaUseCase(_lojaRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveCadastrarLoja()
        {
            // Arrange
            var input = new LojaInputModel
            {
                LojNome = "AutoConnect Veículos",
                LojLogradouro = "Avenida Brasil",
                LojNumero = "1500",
                LojBairro = "Centro",
                LojCidade = "São Paulo",
                LojEstado = "SP",
                LojCEP = "01234567",
                LojEmail = "contato@autoconnect.com.br",
                LojTel1 = "1133334444",
                LojWhatsApp = "11999998888",
                LojCNPJ = "12345678000199",
                LojSts = true
            };

            _lojaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Loja>()))
                .ReturnsAsync(1);

            // Act
            var result = await _useCase.Execute(input);

            // Assert
            result.Should().Be(1);
            _lojaRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Loja>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollbackELancarExcecao()
        {
            // Arrange
            var input = new LojaInputModel
            {
                LojNome = "AutoConnect Veículos",
                LojCidade = "São Paulo"
            };

            _lojaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Loja>()))
                .ThrowsAsync(new Exception("Erro no banco de dados"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Erro no banco de dados");
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Never);
        }

        [Fact]
        public async Task Execute_DeveCriarLojaComDadosCorretos()
        {
            // Arrange
            var input = new LojaInputModel
            {
                LojNome = "Loja Teste",
                LojLogradouro = "Rua Teste",
                LojNumero = "100",
                LojBairro = "Bairro Teste",
                LojCidade = "Cidade Teste",
                LojEstado = "TS",
                LojCEP = "12345678",
                LojComplemento = "Sala 1",
                LojEmail = "loja@teste.com",
                LojTel1 = "1122223333",
                LojTel2 = "1122224444",
                LojWhatsApp = "11999991111",
                LojCNPJ = "98765432000111",
                LojIE = "123456789",
                LojSts = true
            };

            Loja lojaCriada = null;
            _lojaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Loja>()))
                .Callback<Loja>(l => lojaCriada = l)
                .ReturnsAsync(1);

            // Act
            await _useCase.Execute(input);

            // Assert
            lojaCriada.Should().NotBeNull();
            lojaCriada.LojNome.Should().Be("Loja Teste");
            lojaCriada.LojCidade.Should().Be("Cidade Teste");
            lojaCriada.LojCNPJ.Should().Be("98765432000111");
        }
    }
}
