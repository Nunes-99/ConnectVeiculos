using ConnectVeiculos.Application.UseCases.Lojas;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Lojas
{
    public class ExcluirLojaUseCaseTests
    {
        private readonly Mock<ILojaRepository> _lojaRepositoryMock;
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<ILojaUsuarioRepository> _lojaUsuarioRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly ExcluirLojaUseCase _useCase;

        public ExcluirLojaUseCaseTests()
        {
            _lojaRepositoryMock = new Mock<ILojaRepository>();
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _lojaUsuarioRepositoryMock = new Mock<ILojaUsuarioRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new ExcluirLojaUseCase(
                _lojaRepositoryMock.Object,
                _veiculoRepositoryMock.Object,
                _lojaUsuarioRepositoryMock.Object,
                _unitOfWorkMock.Object);
        }

        private static Loja LojaFake() => new Loja(1, "Loja Ativa", "Rua", "100",
            "Bairro", "Cidade", "SP", "12345678", "", "email@loja.com",
            "1122223333", "", "11999999999", "", "12345678000199", "", true);

        [Fact]
        public async Task Execute_ComIdValido_DeveExcluirLojaDeFato()
        {
            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(LojaFake());
            _veiculoRepositoryMock.Setup(x => x.GetByLojaIdAsync(1)).ReturnsAsync(new List<Veiculo>());

            await _useCase.Execute(1);

            _lojaUsuarioRepositoryMock.Verify(x => x.DeleteByLojaIdAsync(1), Times.Once);
            _lojaRepositoryMock.Verify(x => x.DeleteAsync(1), Times.Once);
            _lojaRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Loja>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Loja)null);

            Func<Task> act = async () => await _useCase.Execute(999);

            await act.Should().ThrowAsync<LojaException>().WithMessage("Loja não encontrada.");
            _lojaRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Execute_ComVeiculosVinculados_DeveBloquearExclusao()
        {
            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(LojaFake());
            _veiculoRepositoryMock.Setup(x => x.GetByLojaIdAsync(1))
                .ReturnsAsync(new List<Veiculo> { new Veiculo() });

            Func<Task> act = async () => await _useCase.Execute(1);

            await act.Should().ThrowAsync<LojaException>()
                .WithMessage("*veículo(s) vinculado(s)*");
            _lojaRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
            _lojaUsuarioRepositoryMock.Verify(x => x.DeleteByLojaIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            _lojaRepositoryMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(LojaFake());
            _veiculoRepositoryMock.Setup(x => x.GetByLojaIdAsync(1)).ReturnsAsync(new List<Veiculo>());
            _lojaRepositoryMock.Setup(x => x.DeleteAsync(1)).ThrowsAsync(new Exception("Erro no banco"));

            Func<Task> act = async () => await _useCase.Execute(1);

            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
