using ConnectVeiculos.Application.UseCases.Acessos;
using ConnectVeiculos.Core.Entities.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Acessos
{
    public class InativarAcessoUseCaseTests
    {
        private readonly Mock<IAcessoRepository> _acessoRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly InativarAcessoUseCase _useCase;

        public InativarAcessoUseCaseTests()
        {
            _acessoRepositoryMock = new Mock<IAcessoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new InativarAcessoUseCase(_acessoRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveInativarAcesso()
        {
            // Arrange
            var acesso = new Acesso(1, "Cadastro de Veículos", "Permite cadastrar veículos", true);

            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(acesso);

            Acesso acessoAtualizado = null;
            _acessoRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Acesso>()))
                .Callback<Acesso>(a => acessoAtualizado = a)
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.Execute(1);

            // Assert
            acessoAtualizado.Should().NotBeNull();
            acessoAtualizado.AcsSts.Should().BeFalse();
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            // Arrange
            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Acesso)null);

            // Act
            Func<Task> act = async () => await _useCase.Execute(999);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Acesso nao encontrado.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var acesso = new Acesso(1, "Dashboard", "Acesso ao dashboard", true);

            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(acesso);

            _acessoRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Acesso>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(1);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }

        [Fact]
        public async Task Execute_DeveManterOutrasPropriedades()
        {
            // Arrange
            var acesso = new Acesso(1, "Relatórios", "Acesso aos relatórios", true);

            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(acesso);

            Acesso acessoCapturado = null;
            _acessoRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Acesso>()))
                .Callback<Acesso>(a => acessoCapturado = a)
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.Execute(1);

            // Assert
            acessoCapturado.AcsNome.Should().Be("Relatórios");
            acessoCapturado.AcsDesc.Should().Be("Acesso aos relatórios");
            acessoCapturado.AcsSts.Should().BeFalse();
        }
    }
}
