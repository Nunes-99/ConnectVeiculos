using ConnectVeiculos.Core.Entities.Despesas;
using ConnectVeiculos.Core.Entities.Financeiro;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Services.Financeiro;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    public class FinanceiroServiceTests : IDisposable
    {
        private static readonly DateTime Hoje = new(2026, 11, 20);
        private readonly SqliteConnection _conexao;
        private readonly ConnectVeiculosDbContext _db;
        private readonly FinanceiroService _service;

        public FinanceiroServiceTests()
        {
            _conexao = new SqliteConnection("DataSource=:memory:");
            _conexao.Open();
            _db = new ConnectVeiculosDbContext(new DbContextOptionsBuilder<ConnectVeiculosDbContext>().UseSqlite(_conexao).Options);
            _db.Database.EnsureCreated();
            _db.Lojas.Add(new Loja(1, "Matriz", "", "", "", "", "", "", "", "", "", "", "", "", "", "", true));
            _db.Lojas.Add(new Loja(2, "Filial", "", "", "", "", "", "", "", "", "", "", "", "", "", "", true));
            _db.Categorias.Add(new Core.Entities.Categorias.Categoria(1, "Hatch", "", true));
            _db.Usuarios.Add(new Usuario(1, "Ana", "", "", "ana@x.com", "hash", "Gerente", true));
            // Carro 1 (Matriz) comprado em novembro e vendido; carro 2 (Filial) comprado em outubro.
            _db.Veiculos.Add(new Veiculo(1, 1, 1, "VW - VolksWagen", "GOL", 2020, "ABC1D23", "", "Prata", 1000, 60000m,
                new DateTime(2026, 11, 2), "V", "A", 45000m));
            _db.Veiculos.Add(new Veiculo(2, 2, 1, "FIAT", "ARGO", 2021, "XYZ9K87", "", "Branco", 1000, 70000m,
                new DateTime(2026, 10, 10), "D", "A", 55000m));
            _db.SaveChanges();
            _db.Vendas.Add(new Venda(0, 1, 1, new DateTime(2026, 11, 15), "VW", "GOL", 2020, "", 58000m, 2m, 1160m, "Carlos"));
            _db.VeiculosDespesas.Add(new VeiculoDespesa(0, 1, "Manutencao", "Pneus", 1200m, new DateTime(2026, 11, 5)));
            _db.VeiculosDespesas.Add(new VeiculoDespesa(0, 2, "Estetica", "Polimento", 300m, new DateTime(2026, 11, 8)));
            _db.SaveChanges();
            _service = new FinanceiroService(_db) { Hoje = () => Hoje };
        }

        public void Dispose()
        {
            _db.Dispose();
            _conexao.Dispose();
        }

        private Task<LancamentoInfo> Lancar(string tipo, string descricao, decimal valor, DateTime vencimento,
            DateTime? pagoEm = null, bool recorrente = false, int? loja = null, string categoria = "Aluguel") =>
            _service.CriarAsync(new LancamentoInput
            {
                Tipo = tipo, Descricao = descricao, Categoria = categoria, Valor = valor, Vencimento = vencimento,
                PagoEm = pagoEm, Recorrente = recorrente, LojaId = loja
            }, 1);

        [Fact]
        public async Task Fluxo_do_mes_junta_lancamentos_vendas_compras_e_despesas()
        {
            await Lancar("P", "Aluguel", 5000m, new DateTime(2026, 11, 10), pagoEm: new DateTime(2026, 11, 10));
            await Lancar("P", "Internet", 200m, new DateTime(2026, 11, 25), categoria: "Água/Luz/Internet");
            await Lancar("R", "Parcela do Carlos", 3000m, new DateTime(2026, 11, 28), categoria: "Parcela de cliente");

            var r = await _service.MesAsync(2026, 11, null);

            r.EntradasRealizadas.Should().Be(58000m, "a venda");
            r.SaidasRealizadas.Should().Be(5000m + 45000m + 1200m + 300m, "aluguel + compra do Gol + despesas");
            r.EntradasPrevistas.Should().Be(3000m);
            r.SaidasPrevistas.Should().Be(200m);
            r.SaldoRealizado.Should().Be(58000m - 51500m);
            r.SaldoPrevisto.Should().Be(6500m + 3000m - 200m);
            r.Movimentos.Select(m => m.Origem).Should().Contain(new[] { "venda", "compra", "despesa", "lancamento" });
            r.Movimentos.Should().NotContain(m => m.Descricao.Contains("ARGO") && m.Origem == "compra", "o Argo entrou em outubro");
            r.SaidasPorCategoria.Keys.First().Should().Be("Compra de veículo");
            r.Lancamentos.Should().HaveCount(3);
        }

        [Fact]
        public async Task Filtro_por_loja()
        {
            await Lancar("P", "Aluguel da filial", 2000m, new DateTime(2026, 11, 10), pagoEm: new DateTime(2026, 11, 10), loja: 2);
            await Lancar("P", "Contador", 800m, new DateTime(2026, 11, 10), pagoEm: new DateTime(2026, 11, 10));

            var filial = await _service.MesAsync(2026, 11, 2);

            filial.EntradasRealizadas.Should().Be(0m);
            filial.SaidasRealizadas.Should().Be(2000m + 800m + 300m, "aluguel da filial + conta geral (sem loja) + polimento do Argo");
        }

        [Fact]
        public async Task Atrasadas_de_meses_anteriores_aparecem_e_vencendo_em_breve()
        {
            await Lancar("P", "IPVA atrasado", 1500m, new DateTime(2026, 10, 5));
            await Lancar("P", "Luz", 400m, new DateTime(2026, 11, 22));
            await Lancar("P", "Seguro", 900m, new DateTime(2026, 12, 15));

            var r = await _service.MesAsync(2026, 11, null);

            r.Atrasadas.Should().Be(1);
            r.ValorAtrasado.Should().Be(1500m);
            r.VencemEmBreve.Should().Be(1, "só a luz vence nos próximos 7 dias");
            r.Lancamentos.Select(l => (l.Descricao, l.Situacao)).Should().Contain(("IPVA atrasado", "atrasado"));
            r.Lancamentos.Should().NotContain(l => l.Descricao == "Seguro");
        }

        [Fact]
        public async Task Pagar_recorrente_cria_o_do_mes_seguinte_uma_vez_so()
        {
            var aluguel = await Lancar("P", "Aluguel", 5000m, new DateTime(2026, 11, 10), recorrente: true);

            var pago = await _service.PagarAsync(aluguel.Id, new DateTime(2026, 11, 9));
            pago.Situacao.Should().Be("pago");
            await _service.PagarAsync(aluguel.Id, null);
            await _service.PagarAsync(aluguel.Id, new DateTime(2026, 11, 10));

            var dezembro = await _service.MesAsync(2026, 12, null);
            dezembro.Lancamentos.Should().ContainSingle(l => l.Descricao == "Aluguel" && l.Vencimento == new DateTime(2026, 12, 10) && l.PagoEm == null);
        }

        [Fact]
        public async Task Validacoes_e_exclusao()
        {
            var semValor = () => Lancar("P", "Nada", 0m, Hoje);
            await semValor.Should().ThrowAsync<FinanceiroException>().WithMessage("*maior que zero*");
            var tipo = () => Lancar("X", "Nada", 10m, Hoje);
            await tipo.Should().ThrowAsync<FinanceiroException>();

            var l = await Lancar("P", "Teste", 10m, Hoje);
            (await _service.MesAsync(2026, 11, null)).Lancamentos.Single().Situacao.Should().Be("vence_hoje");
            await _service.AlterarAsync(l.Id, new LancamentoInput { Tipo = "R", Descricao = "Teste 2", Valor = 20m, Vencimento = Hoje.AddDays(3) });
            (await _service.MesAsync(2026, 11, null)).Lancamentos.Single().Should().Match<LancamentoInfo>(x => x.Tipo == "R" && x.Valor == 20m);
            await _service.ExcluirAsync(l.Id);
            (await _service.MesAsync(2026, 11, null)).Lancamentos.Should().BeEmpty();
            var inexistente = () => _service.ExcluirAsync(999);
            await inexistente.Should().ThrowAsync<FinanceiroException>();
        }
    }
}
