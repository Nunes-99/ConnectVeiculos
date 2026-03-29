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
    public class AtualizarAcessoUseCaseTests
    {
        private readonly Mock<IAcessoRepository> _acessoRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly AtualizarAcessoUseCase _useCase;

        public AtualizarAcessoUseCaseTests()
        {
            _acessoRepositoryMock = new Mock<IAcessoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new AtualizarAcessoUseCase(_acessoRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveAtualizarAcesso()
        {
            // Arrange
            var acessoExistente = new Acesso(1, "Acesso Antigo", "Descrição antiga", true);

            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(acessoExistente);

            var input = new AcessoInputModel
            {
                AcsId = 1,
                AcsNome = "Acesso Atualizado",
                AcsDesc = "Nova descrição",
                AcsSts = true
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            _acessoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Acesso>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComAcessoInexistente_DeveLancarExcecao()
        {
            // Arrange
            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Acesso)null);

            var input = new AcessoInputModel { AcsId = 999, AcsNome = "Teste" };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Acesso nao encontrado.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var acessoExistente = new Acesso(1, "Acesso", "Descrição", true);

            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(acessoExistente);

            _acessoRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Acesso>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            var input = new AcessoInputModel { AcsId = 1, AcsNome = "Acesso" };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }

        [Fact]
        public async Task Execute_DeveAtualizarPropriedadesCorretas()
        {
            // Arrange
            var acessoExistente = new Acesso(1, "Nome Original", "Desc Original", true);

            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(acessoExistente);

            Acesso acessoAtualizado = null;
            _acessoRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Acesso>()))
                .Callback<Acesso>(a => acessoAtualizado = a)
                .Returns(Task.CompletedTask);

            var input = new AcessoInputModel
            {
                AcsId = 1,
                AcsNome = "Nome Novo",
                AcsDesc = "Desc Nova",
                AcsSts = false
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            acessoAtualizado.Should().NotBeNull();
            acessoAtualizado.AcsNome.Should().Be("Nome Novo");
            acessoAtualizado.AcsDesc.Should().Be("Desc Nova");
            acessoAtualizado.AcsSts.Should().BeFalse();
        }
    }
}
