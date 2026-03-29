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
    public class AtualizarLojaUseCaseTests
    {
        private readonly Mock<ILojaRepository> _lojaRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly AtualizarLojaUseCase _useCase;

        public AtualizarLojaUseCaseTests()
        {
            _lojaRepositoryMock = new Mock<ILojaRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new AtualizarLojaUseCase(_lojaRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveAtualizarLoja()
        {
            // Arrange
            var lojaExistente = new Loja(1, "Loja Antiga", "Rua Antiga", "100",
                "Bairro", "Cidade", "SP", "12345678", "", "email@loja.com",
                "1122223333", "", "11999999999", "", "12345678000199", "", true);

            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(lojaExistente);

            var input = new LojaInputModel
            {
                LojId = 1,
                LojNome = "Loja Atualizada",
                LojLogradouro = "Nova Rua",
                LojNumero = "200",
                LojBairro = "Novo Bairro",
                LojCidade = "Nova Cidade",
                LojEstado = "RJ",
                LojCEP = "87654321",
                LojEmail = "novo@loja.com",
                LojTel1 = "2133334444",
                LojWhatsApp = "21988887777",
                LojCNPJ = "12345678000199",
                LojSts = true
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            _lojaRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Loja>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComLojaInexistente_DeveLancarExcecao()
        {
            // Arrange
            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Loja)null);

            var input = new LojaInputModel { LojId = 999, LojNome = "Loja Inexistente" };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Loja nao encontrada.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var lojaExistente = new Loja(1, "Loja Antiga", "Rua Antiga", "100",
                "Bairro", "Cidade", "SP", "12345678", "", "email@loja.com",
                "1122223333", "", "11999999999", "", "12345678000199", "", true);

            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(lojaExistente);

            _lojaRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Loja>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            var input = new LojaInputModel
            {
                LojId = 1,
                LojNome = "Loja Atualizada"
            };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
