using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Favoritos;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Services.Notificacao;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Garante que o link enviado por e-mail bate com a rota publica do Angular
    /// (/catalogo/{tenantSlug}/veiculo/{id}). Ja quebrou uma vez: o link era
    /// /catalogo/veiculo/{id}, que cai no wildcard do router e manda o
    /// destinatario pra landing page em vez do veiculo.
    /// </summary>
    public class FavoritoNotificacaoServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ConnectVeiculosDbContext _context;
        private readonly Mock<IEmailService> _emailServiceMock;

        public FavoritoNotificacaoServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ConnectVeiculosDbContext(options);
            _context.Database.EnsureCreated();

            // Loja e Categoria sao FK obrigatoria de Veiculo.
            _context.Lojas.Add(new Loja(1, "Loja Teste", "Rua A", "1", "Centro", "Sao Paulo",
                "SP", "01310100", null, "loja@teste.com", "1133334444", null, "11999998888",
                null, "11222333000181", null, true, lojSlug: "centro"));
            _context.Categorias.Add(new Categoria(1, "Hatch", "Carros hatch", true));
            _context.Categorias.Add(new Categoria(2, "SUV", "Utilitarios", true));
            _context.SaveChanges();

            _emailServiceMock = new Mock<IEmailService>();
        }

        private FavoritoNotificacaoService CriarServico(string tenantSlug)
        {
            var tenantContextMock = new Mock<ITenantContext>();
            tenantContextMock.SetupGet(t => t.IsResolved).Returns(!string.IsNullOrEmpty(tenantSlug));
            tenantContextMock.SetupGet(t => t.TenantSlug).Returns(tenantSlug);

            return new FavoritoNotificacaoService(
                _context,
                _emailServiceMock.Object,
                tenantContextMock.Object,
                NullLogger<FavoritoNotificacaoService>.Instance);
        }

        private Veiculo SemearVeiculo(int veiId, decimal preco, string marca = "Volkswagen", int catId = 1)
        {
            var veiculo = new Veiculo(veiId, 1, catId, marca, "Golf GTI", 2022, "ABC1D23",
                "9BWZZZ372VT004251", "Branco", 25000, preco, DateTime.UtcNow, "D", "Usado", preco - 10000);
            _context.Veiculos.Add(veiculo);
            _context.SaveChanges();
            return veiculo;
        }

        [Fact]
        public async Task NotificarPrecoAlterado_DeveEnviarLinkComTenantSlugEIdDoVeiculo()
        {
            SemearVeiculo(10, 90_000m);
            _context.Favoritos.Add(new Favorito(1, 10, "cliente@teste.com", "Cliente", "11999998888"));
            _context.SaveChanges();

            string linkEnviado = null;
            _emailServiceMock
                .Setup(e => e.SendPrecoAlteradoAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>()))
                .Callback<string, string, string, decimal, decimal, string>((_, _, _, _, _, link) => linkEnviado = link)
                .ReturnsAsync(true);

            await CriarServico("acme").NotificarPrecoAlteradoAsync(10, 100_000m, 90_000m);

            linkEnviado.Should().EndWith("/catalogo/acme/veiculo/10");
        }

        [Fact]
        public async Task NotificarVeiculoSimilar_DeveEnviarLinkComTenantSlugEIdDoVeiculo()
        {
            // Similar = mesma marca, mesma categoria, preco dentro de +-20%.
            SemearVeiculo(20, 100_000m);
            SemearVeiculo(21, 105_000m);
            _context.Favoritos.Add(new Favorito(1, 21, "interessado@teste.com", "Interessado", "11999998888"));
            _context.SaveChanges();

            string linkEnviado = null;
            _emailServiceMock
                .Setup(e => e.SendVeiculoSimilarAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()))
                .Callback<string, string, string, decimal, string>((_, _, _, _, link) => linkEnviado = link)
                .ReturnsAsync(true);

            await CriarServico("acme").NotificarVeiculoSimilarAsync(20);

            linkEnviado.Should().EndWith("/catalogo/acme/veiculo/20");
        }

        [Fact]
        public async Task NotificarPrecoAlterado_SemTenantResolvido_NaoDeveMontarLinkQuebrado()
        {
            SemearVeiculo(30, 90_000m);
            _context.Favoritos.Add(new Favorito(1, 30, "cliente@teste.com", "Cliente", "11999998888"));
            _context.SaveChanges();

            string linkEnviado = null;
            _emailServiceMock
                .Setup(e => e.SendPrecoAlteradoAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>()))
                .Callback<string, string, string, decimal, decimal, string>((_, _, _, _, _, link) => linkEnviado = link)
                .ReturnsAsync(true);

            await CriarServico(string.Empty).NotificarPrecoAlteradoAsync(30, 100_000m, 90_000m);

            // Sem slug o link vai pro catalogo raiz — nunca pro /catalogo/veiculo/{id},
            // que e' exatamente a rota inexistente que causou o bug.
            linkEnviado.Should().EndWith("/catalogo");
            linkEnviado.Should().NotContain("/catalogo/veiculo/");
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
