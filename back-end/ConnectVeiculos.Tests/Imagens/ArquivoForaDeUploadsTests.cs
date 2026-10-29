using ConnectVeiculos.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Imagens
{
    /// <summary>
    /// GET /api/imagens/file e' publica. Ela juntava o parametro path direto ao
    /// ContentRoot: "../app/data/cliente.db" baixava o banco da loja sem login.
    /// So pode sair imagem, e so de dentro de uploads/.
    /// </summary>
    public class ArquivoForaDeUploadsTests : IDisposable
    {
        private readonly string _raiz;
        private readonly ImagensController _controller;

        public ArquivoForaDeUploadsTests()
        {
            _raiz = Path.Combine(Path.GetTempPath(), "cv-trav-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Path.Combine(_raiz, "uploads", "veiculos", "1"));
            Directory.CreateDirectory(Path.Combine(_raiz, "data"));
            File.WriteAllText(Path.Combine(_raiz, "data", "cliente.db"), "banco");
            File.WriteAllText(Path.Combine(_raiz, "data", "foto.jpg"), "fora de uploads");
            File.WriteAllText(Path.Combine(_raiz, "uploads", "segredo.db"), "nao e imagem");
            File.WriteAllText(Path.Combine(_raiz, "uploads", "veiculos", "1", "a.jpg"), "jpeg");

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(_raiz);
            _controller = new ImagensController(env.Object);
        }

        [Theory]
        [InlineData("data/cliente.db")]
        [InlineData("/data/cliente.db")]
        [InlineData("uploads/../data/cliente.db")]
        [InlineData("/uploads/../../etc/passwd")]
        [InlineData("../data/cliente.db")]
        [InlineData("data/foto.jpg")]                 // imagem, mas fora de uploads/
        [InlineData("/uploads/segredo.db")]           // dentro de uploads/, mas nao e imagem
        [InlineData("uploads")]
        public async Task Nao_serve_nada_fora_de_uploads_nem_o_que_nao_e_imagem(string path)
        {
            var r = await _controller.GetImageFile(path);

            r.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Foto_do_veiculo_continua_saindo()
        {
            var r = await _controller.GetImageFile("/uploads/veiculos/1/a.jpg");

            r.Should().BeOfType<PhysicalFileResult>()
                .Which.ContentType.Should().Be("image/jpeg");
        }

        public void Dispose()
        {
            try { Directory.Delete(_raiz, true); } catch { }
        }
    }
}
