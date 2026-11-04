using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.NotasFiscais;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.NotasFiscais;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ConnectVeiculos.Tests.NotasFiscais
{
    /// <summary>Filtros da tela Notas fiscais rodando no SQLite de verdade.</summary>
    public class NotaFiscalRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ConnectVeiculosDbContext _context;
        private readonly NotaFiscalRepository _repo;

        public NotaFiscalRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _context = new ConnectVeiculosDbContext(new DbContextOptionsBuilder<ConnectVeiculosDbContext>().UseSqlite(_connection).Options);
            _context.Database.EnsureCreated();

            _context.Lojas.Add(new Loja(1, "Loja Teste", "Rua A", "1", "Centro", "Salto",
                "SP", "13320000", null, "loja@teste.com", "1133334444", null, "11999998888",
                null, "11222333000181", null, true, lojSlug: "centro"));
            _context.Categorias.Add(new Categoria(1, "Hatch", "Carros hatch", true));
            _context.Veiculos.Add(new Veiculo(1, 1, 1, "Volkswagen", "Golf GTI", 2022, "ABC1D23",
                "9BWZZZ372VT004251", "Branco", 25000, 150000m, DateTime.UtcNow, "D", "Usado", 140000m));
            _context.Veiculos.Add(new Veiculo(2, 1, 1, "Toyota", "Corolla", 2020, "XYZ9K87",
                "9BRBL3HE0L0123456", "Prata", 60000, 90000m, DateTime.UtcNow, "D", "Usado", 80000m));
            _context.SaveChanges();

            Nota(1, NotaFiscal.Entrada, 10, new DateTime(2026, 10, 5, 14, 0, 0), "Joao Vendedor", autorizada: true);
            Nota(1, NotaFiscal.Saida, 11, new DateTime(2026, 11, 2, 23, 30, 0), "Maria Souza", autorizada: true);
            Nota(2, NotaFiscal.Saida, 12, new DateTime(2026, 11, 3, 9, 0, 0), "Carlos Lima", autorizada: false);
            Nota(99, NotaFiscal.Entrada, 13, new DateTime(2026, 11, 1, 9, 0, 0), "Veiculo Excluido", autorizada: true);
            _repo = new NotaFiscalRepository(_context);
        }

        private void Nota(int veiculoId, string tipo, int numero, DateTime emissao, string destinatario, bool autorizada)
        {
            var nota = new NotaFiscal(veiculoId, tipo, "homologacao", 1, numero);
            nota.PrepararEnvio($"{numero:D44}", 1000m, "52998224725", destinatario, "{}", emissao);
            if (autorizada) nota.Autorizar(100, "Autorizado o uso da NF-e", "13526000000" + numero, emissao, "<nfeProc/>");
            else nota.Rejeitar(539, "Duplicidade");
            _context.NotasFiscais.Add(nota);
            _context.SaveChanges();
        }

        private async Task<int[]> Numeros(NfeFiltro filtro)
            => (await _repo.ListarAsync(filtro, 500)).Select(x => x.Nota.NfNumero).ToArray();

        [Fact]
        public async Task Sem_Filtro_Traz_Todas_Da_Mais_Recente_Com_O_Veiculo()
        {
            var lista = await _repo.ListarAsync(new NfeFiltro(), 500);

            lista.Select(x => x.Nota.NfNumero).Should().Equal(12, 11, 13, 10);
            lista[0].Marca.Should().Be("Toyota");
            lista[0].Placa.Should().Be("XYZ9K87");
            lista[2].Marca.Should().BeNull("o veiculo 99 nao existe, mas a nota continua na lista");
        }

        [Fact]
        public async Task Periodo_Inclui_O_Dia_Final_Inteiro()
        {
            (await Numeros(new NfeFiltro { De = new DateTime(2026, 11, 1), Ate = new DateTime(2026, 11, 2) }))
                .Should().Equal(11, 13);
        }

        [Fact]
        public async Task Filtra_Por_Tipo_E_Situacao()
        {
            (await Numeros(new NfeFiltro { Tipo = NotaFiscal.Saida })).Should().Equal(12, 11);
            (await Numeros(new NfeFiltro { Status = NotaFiscal.StatusRejeitada })).Should().Equal(12);
        }

        [Theory]
        [InlineData("11", new[] { 11 })]
        [InlineData("maria", new[] { 11 })]
        [InlineData("abc-1d23", new[] { 11, 10 })]
        [InlineData("corolla", new[] { 12 })]
        public async Task Busca_Por_Numero_Destinatario_Placa_E_Veiculo(string busca, int[] esperado)
        {
            (await Numeros(new NfeFiltro { Busca = busca })).Should().Equal(esperado);
        }

        [Fact]
        public async Task Respeita_O_Limite()
        {
            (await _repo.ListarAsync(new NfeFiltro(), 2)).Should().HaveCount(2);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
