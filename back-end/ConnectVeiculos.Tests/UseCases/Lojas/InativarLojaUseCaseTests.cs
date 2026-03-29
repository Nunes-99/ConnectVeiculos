using ConnectVeiculos.Application.UseCases.Lojas;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Lojas
{
    public class InativarLojaUseCaseTests
    {
        private readonly Mock<ILojaRepository> _lojaRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly InativarLojaUseCase _useCase;

        public InativarLojaUseCaseTests()
        {
            _lojaRepositoryMock = new Mock<ILojaRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new InativarLojaUseCase(_lojaRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveInativarLoja()
        {
            // Arrange
            var loja = new Loja(1, "Loja Ativa", "Rua", "100",
                "Bairro", "Cidade", "SP", "12345678", "", "email@loja.com",
                "1122223333", "", "11999999999", "", "12345678000199", "", true);

            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(loja);

            Loja lojaAtualizada = null;
            _lojaRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Loja>()))
                .Callback<Loja>(l => lojaAtualizada = l)
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.Execute(1);

            // Assert
            lojaAtualizada.Should().NotBeNull();
            lojaAtualizada.LojSts.Should().BeFalse();
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            // Arrange
            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Loja)null);

            // Act
            Func<Task> act = async () => await _useCase.Execute(999);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Loja nao encontrada.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var loja = new Loja(1, "Loja Ativa", "Rua", "100",
                "Bairro", "Cidade", "SP", "12345678", "", "email@loja.com",
                "1122223333", "", "11999999999", "", "12345678000199", "", true);

            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(loja);

            _lojaRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Loja>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(1);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
