using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Services.PrimeirosPassos;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    public class PrimeirosPassosServiceTests : IDisposable
    {
        private sealed class ConfigEmMemoria : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> Valores = new();
            public Task<string> GetValorAsync(string chave) => Task.FromResult(Valores.TryGetValue(chave, out var v) ? v : null!);
            public Task SetValorAsync(string chave, string valor) { Valores[chave] = valor; return Task.CompletedTask; }
        }

        private readonly SqliteConnection _conexao;
        private readonly ConnectVeiculosDbContext _db;
        private readonly ConfigEmMemoria _config = new();
        private readonly PrimeirosPassosService _service;

        public PrimeirosPassosServiceTests()
        {
            _conexao = new SqliteConnection("DataSource=:memory:");
            _conexao.Open();
            _db = new ConnectVeiculosDbContext(new DbContextOptionsBuilder<ConnectVeiculosDbContext>().UseSqlite(_conexao).Options);
            _db.Database.EnsureCreated();
            _db.Categorias.Add(new Core.Entities.Categorias.Categoria(1, "Hatch", "", true));
            _db.Usuarios.Add(new Usuario(1, "Dono", "", "", "dono@x.com", "hash", "Administrador", true));
            _service = new PrimeirosPassosService(_db, _config);
        }

        public void Dispose()
        {
            _db.Dispose();
            _conexao.Dispose();
        }

        private static Loja LojaNova(string? whats = null, string? cep = null, string? img = null, string? cor = null) =>
            new(1, "Loja", "Rua", "1", "Centro", whats == null ? "" : "Niterói", "RJ", cep ?? "", "", "", "", "", whats ?? "", img ?? "", "", "", true,
                lojCorPrimaria: cor);

        [Fact]
        public async Task Loja_nova_comeca_com_tudo_por_fazer()
        {
            _db.Lojas.Add(LojaNova());
            _db.SaveChanges();

            var r = await _service.ConsultarAsync();

            r.Total.Should().Be(7);
            r.Feitos.Should().Be(0);
            r.Concluido.Should().BeFalse();
            r.Oculto.Should().BeFalse();
            r.Passos.Select(p => p.Id).Should().Equal("loja", "veiculo", "site", "redes", "portais", "equipe", "venda");
        }

        [Fact]
        public async Task Cada_passo_se_marca_pelo_que_esta_cadastrado()
        {
            _db.Lojas.Add(LojaNova(whats: "21999998888", cep: "24230090", img: "uploads/logo.png", cor: "#c62828"));
            _db.Usuarios.Add(new Usuario(2, "Vendedor", "", "", "v@x.com", "hash", "Vendedor", true));
            _db.SaveChanges();
            _db.Veiculos.Add(new Veiculo(1, 1, 1, "VW", "GOL", 2020, "ABC1D23", "", "Prata", 1000, 50000m, DateTime.Today, "V", "A", 40000m));
            _db.SaveChanges();
            _db.VeiculosImagens.Add(new VeiculoImagem(0, 1, "uploads/veiculos/1/a.jpg", 0, true));
            _db.Vendas.Add(new Venda(0, 1, 1, DateTime.Today, "VW", "GOL", 2020, "", 50000m, 0, 0, "Cliente"));
            _db.SaveChanges();
            _config.Valores["META_PAGE_TOKEN_CIFRADO"] = "x";
            _config.Valores["NAPISTA_TOKEN"] = "x";

            var r = await _service.ConsultarAsync();

            r.Passos.Where(p => !p.Feito).Select(p => p.Id).Should().BeEmpty();
            r.Concluido.Should().BeTrue();
        }

        [Fact]
        public async Task Cor_padrao_nao_conta_como_site_personalizado_e_usuario_inativo_nao_conta_como_equipe()
        {
            _db.Lojas.Add(LojaNova(cor: "#1A237E"));
            _db.Usuarios.Add(new Usuario(2, "Ex-vendedor", "", "", "ex@x.com", "hash", "Vendedor", false));
            _db.SaveChanges();

            var r = await _service.ConsultarAsync();

            r.Passos.Single(p => p.Id == "site").Feito.Should().BeFalse();
            r.Passos.Single(p => p.Id == "equipe").Feito.Should().BeFalse();
        }

        [Fact]
        public async Task Ocultar_e_mostrar_de_novo()
        {
            (await _service.OcultarAsync(true)).Oculto.Should().BeTrue();
            _config.Valores[PrimeirosPassosService.ChaveOculto].Should().Be("1");
            (await _service.OcultarAsync(false)).Oculto.Should().BeFalse();
        }
    }
}
