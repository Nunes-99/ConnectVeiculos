using ConnectVeiculos.Application.InputModels.Acessos;
using ConnectVeiculos.Application.UseCases.Acessos;
using ConnectVeiculos.Core.Entities.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Acessos
{
    public class CadastrarAcessoUseCaseTests
    {
        private readonly Mock<IAcessoRepository> _acessoRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly CadastrarAcessoUseCase _useCase;

        public CadastrarAcessoUseCaseTests()
        {
            _acessoRepositoryMock = new Mock<IAcessoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new CadastrarAcessoUseCase(_acessoRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveCadastrarAcesso()
        {
            // Arrange
            var input = new AcessoInputModel
            {
                AcsNome = "Cadastro de Veículos",
                AcsDesc = "Permite cadastrar novos veículos",
                AcsSts = true
            };

            _acessoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Acesso>()))
                .ReturnsAsync(1);

            // Act
            var result = await _useCase.Execute(input);

            // Assert
            result.Should().Be(1);
            _acessoRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Acesso>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var input = new AcessoInputModel
            {
                AcsNome = "Relatórios",
                AcsDesc = "Acesso a relatórios"
            };

            _acessoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Acesso>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }

        [Fact]
        public async Task Execute_DeveCriarAcessoComDadosCorretos()
        {
            // Arrange
            var input = new AcessoInputModel
            {
                AcsNome = "Dashboard",
                AcsDesc = "Acesso ao dashboard",
                AcsSts = true
            };

            Acesso acessoCriado = null;
            _acessoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Acesso>()))
                .Callback<Acesso>(a => acessoCriado = a)
                .ReturnsAsync(1);

            // Act
            await _useCase.Execute(input);

            // Assert
            acessoCriado.Should().NotBeNull();
            acessoCriado.AcsNome.Should().Be("Dashboard");
            acessoCriado.AcsDesc.Should().Be("Acesso ao dashboard");
        }
    }
}
