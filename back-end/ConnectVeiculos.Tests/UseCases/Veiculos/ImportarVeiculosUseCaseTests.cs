using ConnectVeiculos.Application.UseCases.Veiculos;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Text;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Veiculos
{
    public class ImportarVeiculosUseCaseTests
    {
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<ICategoriaRepository> _categoriaRepositoryMock;
        private readonly ImportarVeiculosUseCase _useCase;

        public ImportarVeiculosUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _categoriaRepositoryMock = new Mock<ICategoriaRepository>();
            _useCase = new ImportarVeiculosUseCase(_veiculoRepositoryMock.Object, _categoriaRepositoryMock.Object);
        }

        [Fact]
        public async Task Execute_ComArquivoFormatoInvalido_DeveRetornarErro()
        {
            // Arrange
            var arquivo = CriarArquivoMock("veiculos.txt", "conteudo");

            // Act
            var result = await _useCase.Execute(arquivo, 1);

            // Assert
            result.Erros.Should().Be(1);
            result.Detalhes.First().Mensagem.Should().Contain("Formato de arquivo não suportado");
        }

        [Fact]
        public async Task Execute_ComCsvValido_DeveImportarVeiculos()
        {
            // Arrange
            var csvContent = "marca;modelo;ano;placa;preco\nToyota;Corolla;2023;ABC1234;80000";
            var arquivo = CriarArquivoMock("veiculos.csv", csvContent);

            var categorias = new List<Categoria>
            {
                new Categoria(1, "Sedan", "Veículos Sedan", true)
            };

            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(categorias);
            _veiculoRepositoryMock.Setup(x => x.GetByPlacaAsync(It.IsAny<string>())).ReturnsAsync((Veiculo)null);
            _veiculoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Veiculo>())).ReturnsAsync(1);

            // Act
            var result = await _useCase.Execute(arquivo, 1);

            // Assert
            result.Importados.Should().Be(1);
            result.Erros.Should().Be(0);
        }

        [Fact]
        public async Task Execute_ComPlacaDuplicada_DeveRetornarErro()
        {
            // Arrange
            var csvContent = "marca;modelo;ano;placa;preco\nToyota;Corolla;2023;ABC1234;80000";
            var arquivo = CriarArquivoMock("veiculos.csv", csvContent);

            var categorias = new List<Categoria>
            {
                new Categoria(1, "Sedan", "Veículos Sedan", true)
            };

            var veiculoExistente = new Veiculo(1, 1, 1, "Honda", "Civic", 2022, "ABC1234", "CHASSI", "Preto", 0, 70000m, DateTime.Now, "D", "A", 60000m);

            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(categorias);
            _veiculoRepositoryMock.Setup(x => x.GetByPlacaAsync("ABC1234")).ReturnsAsync(veiculoExistente);

            // Act
            var result = await _useCase.Execute(arquivo, 1);

            // Assert
            result.Erros.Should().Be(1);
            result.Detalhes.First().Mensagem.Should().Contain("já existe");
        }

        [Fact]
        public async Task Execute_SemMarcaObrigatoria_DeveRetornarErro()
        {
            // Arrange
            var csvContent = "marca;modelo;ano;placa;preco\n;Corolla;2023;ABC1234;80000";
            var arquivo = CriarArquivoMock("veiculos.csv", csvContent);

            var categorias = new List<Categoria>();
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(categorias);

            // Act
            var result = await _useCase.Execute(arquivo, 1);

            // Assert
            result.Erros.Should().Be(1);
            result.Detalhes.First().Mensagem.Should().Contain("Marca é obrigatória");
        }

        [Fact]
        public async Task Execute_SemModeloObrigatorio_DeveRetornarErro()
        {
            // Arrange
            var csvContent = "marca;modelo;ano;placa;preco\nToyota;;2023;ABC1234;80000";
            var arquivo = CriarArquivoMock("veiculos.csv", csvContent);

            var categorias = new List<Categoria>();
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(categorias);

            // Act
            var result = await _useCase.Execute(arquivo, 1);

            // Assert
            result.Erros.Should().Be(1);
            result.Detalhes.First().Mensagem.Should().Contain("Modelo é obrigatório");
        }

        [Fact]
        public async Task Execute_ComCategoriaNome_DeveMapeaParaId()
        {
            // Arrange
            var csvContent = "marca;modelo;ano;placa;preco;categoria\nToyota;Corolla;2023;ABC1234;80000;Sedan";
            var arquivo = CriarArquivoMock("veiculos.csv", csvContent);

            var categorias = new List<Categoria>
            {
                new Categoria(5, "Sedan", "Veículos Sedan", true)
            };

            Veiculo veiculoCriado = null;
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(categorias);
            _veiculoRepositoryMock.Setup(x => x.GetByPlacaAsync(It.IsAny<string>())).ReturnsAsync((Veiculo)null);
            _veiculoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Veiculo>()))
                .Callback<Veiculo>(v => veiculoCriado = v)
                .ReturnsAsync(1);

            // Act
            await _useCase.Execute(arquivo, 1);

            // Assert
            veiculoCriado.Should().NotBeNull();
            veiculoCriado.R_CatId.Should().Be(5);
        }

        [Fact]
        public async Task Execute_DeveUsarLojaIdInformado()
        {
            // Arrange
            var csvContent = "marca;modelo;ano;placa;preco\nToyota;Corolla;2023;ABC1234;80000";
            var arquivo = CriarArquivoMock("veiculos.csv", csvContent);

            Veiculo veiculoCriado = null;
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());
            _veiculoRepositoryMock.Setup(x => x.GetByPlacaAsync(It.IsAny<string>())).ReturnsAsync((Veiculo)null);
            _veiculoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Veiculo>()))
                .Callback<Veiculo>(v => veiculoCriado = v)
                .ReturnsAsync(1);

            // Act
            await _useCase.Execute(arquivo, 10);

            // Assert
            veiculoCriado.Should().NotBeNull();
            veiculoCriado.R_LojId.Should().Be(10);
        }

        [Fact]
        public async Task Execute_ComMultiplasLinhas_DeveProcessarTodas()
        {
            // Arrange
            var csvContent = "marca;modelo;ano;placa;preco\nToyota;Corolla;2023;ABC1234;80000\nHonda;Civic;2022;DEF5678;90000\nFord;Focus;2021;GHI9012;70000";
            var arquivo = CriarArquivoMock("veiculos.csv", csvContent);

            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());
            _veiculoRepositoryMock.Setup(x => x.GetByPlacaAsync(It.IsAny<string>())).ReturnsAsync((Veiculo)null);
            _veiculoRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Veiculo>())).ReturnsAsync(1);

            // Act
            var result = await _useCase.Execute(arquivo, 1);

            // Assert
            result.TotalLinhas.Should().Be(3);
            result.Importados.Should().Be(3);
        }

        private IFormFile CriarArquivoMock(string fileName, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(bytes.Length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

            return fileMock.Object;
        }
    }
}
