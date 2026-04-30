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
        private readonly Mock<IAcessoRepository> _acessoRepositoryMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly InativarAcessoUseCase _useCase;

        public InativarAcessoUseCaseTests()
        {
            _useCase = new InativarAcessoUseCase(_acessoRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveExcluirAcesso()
        {
            var acesso = new Acesso(1, "Cadastro de Veículos", "Permite cadastrar veículos", true);
            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(acesso);

            await _useCase.Execute(1);

            _acessoRepositoryMock.Verify(x => x.DeleteAsync(1), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Acesso)null!);

            Func<Task> act = async () => await _useCase.Execute(999);

            await act.Should().ThrowAsync<Exception>().WithMessage("*Acesso nao encontrado*");
            _acessoRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            var acesso = new Acesso(1, "X", "Y", true);
            _acessoRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(acesso);
            _acessoRepositoryMock.Setup(x => x.DeleteAsync(1)).ThrowsAsync(new Exception("Erro"));

            Func<Task> act = async () => await _useCase.Execute(1);

            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
