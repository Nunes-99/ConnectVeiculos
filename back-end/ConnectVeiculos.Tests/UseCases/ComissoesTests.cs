using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.UseCases.Comissoes;
using ConnectVeiculos.Core.Entities.Despesas;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Despesas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases
{
    public class ComissoesTests
    {
        // ---------------- Calculo ----------------

        [Fact]
        public void Percentual_da_venda()
        {
            var c = ComissaoCalculo.Calcular(new RegraComissao { Tipo = TipoComissao.PercentualVenda, Valor = 2 }, 80000m, 60000m);
            c.Valor.Should().Be(1600m);
            c.Percentual.Should().Be(2m);
            c.Regra.Should().Be("2% da venda");
        }

        [Fact]
        public void Percentual_do_lucro_e_zero_se_vendeu_no_prejuizo()
        {
            var regra = new RegraComissao { Tipo = TipoComissao.PercentualLucro, Valor = 10 };
            var c = ComissaoCalculo.Calcular(regra, 80000m, 70000m);
            c.Valor.Should().Be(1000m);
            c.Percentual.Should().Be(1.25m);
            c.Regra.Should().StartWith("10% do lucro");

            ComissaoCalculo.Calcular(regra, 60000m, 70000m).Valor.Should().Be(0m);
        }

        [Fact]
        public void Valor_fixo_por_carro()
        {
            var c = ComissaoCalculo.Calcular(new RegraComissao { Tipo = TipoComissao.ValorFixo, Valor = 500 }, 50000m, 0m);
            c.Valor.Should().Be(500m);
            c.Percentual.Should().Be(1m);
            c.Regra.Should().Contain("por carro");
        }

        [Theory]
        [InlineData("OUTRO", 5)]
        [InlineData(TipoComissao.PercentualVenda, -1)]
        [InlineData(TipoComissao.PercentualLucro, 101)]
        public void Regra_invalida(string tipo, decimal valor)
        {
            var acao = () => ComissaoCalculo.Validar(new RegraComissao { Tipo = tipo, Valor = valor }, "x");
            acao.Should().Throw<InputModelException>();
        }

        // ---------------- Regras, sugestao, extrato ----------------

        private sealed class ConfigEmMemoria : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> Valores = new();
            public Task<string> GetValorAsync(string chave) => Task.FromResult(Valores.TryGetValue(chave, out var v) ? v : null!);
            public Task SetValorAsync(string chave, string valor) { Valores[chave] = valor; return Task.CompletedTask; }
        }

        private readonly List<Venda> _vendas = new();
        private readonly ComissoesUseCase _uc;

        private static Venda Venda(int id, int vendedor, DateTime data, decimal valor, decimal porc, string status = "A")
        {
            var v = new Venda(id, id, vendedor, data, "VW - VolksWagen", "GOL", 2020, "", valor, porc, valor * porc / 100m, "Cliente " + id);
            if (status == "E") v.Estornar();
            return v;
        }

        public ComissoesTests()
        {
            var vendas = new Mock<IVendaRepository>();
            vendas.Setup(v => v.GetAllAsync()).ReturnsAsync(() => _vendas);
            vendas.Setup(v => v.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => _vendas.FirstOrDefault(v => v.VenId == id)!);
            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetByIdAsync(9)).ReturnsAsync(new Veiculo(9, 1, 1, "VW", "GOL", 2020, "ABC1D23", "", "Prata", 1000, 60000m,
                DateTime.Today, "D", "A", 45000m));
            var despesas = new Mock<IVeiculoDespesaRepository>();
            despesas.Setup(d => d.GetByVeiculoIdAsync(9)).ReturnsAsync(new[] { new VeiculoDespesa(1, 9, "Manutencao", "Pneus", 3000m, DateTime.Today) });
            var usuarios = new Mock<IUsuarioRepository>();
            usuarios.Setup(u => u.GetAllAsync()).ReturnsAsync(new[]
            {
                new Usuario(1, "Ana", "", "", "ana@x.com", "hash-teste", "Vendedor", true),
                new Usuario(2, "Bruno", "", "", "bruno@x.com", "hash-teste", "Vendedor", true)
            });
            _uc = new ComissoesUseCase(vendas.Object, veiculos.Object, despesas.Object, usuarios.Object, new ConfigEmMemoria());
        }

        [Fact]
        public async Task Sugestao_usa_a_regra_do_vendedor_ou_a_padrao_e_o_custo_com_despesas()
        {
            (await _uc.SugestaoAsync(1, 9, 58000m)).Should().BeNull("sem regras a venda fica com o % digitado");

            await _uc.SalvarRegrasAsync(new RegrasComissao
            {
                Padrao = new RegraComissao { Tipo = TipoComissao.PercentualVenda, Valor = 1 },
                Vendedores = new() { [2] = new RegraComissao { Tipo = TipoComissao.PercentualLucro, Valor = 10 } }
            });

            var ana = (await _uc.SugestaoAsync(1, 9, 58000m))!;
            ana.Valor.Should().Be(580m);
            ana.RegraDoVendedor.Should().BeFalse();

            var bruno = (await _uc.SugestaoAsync(2, 9, 58000m))!;
            bruno.Valor.Should().Be(1000m, "10% de (58.000 - 45.000 - 3.000)");
            bruno.RegraDoVendedor.Should().BeTrue();
        }

        [Fact]
        public async Task Regra_de_usuario_inexistente_e_recusada()
        {
            var acao = () => _uc.SalvarRegrasAsync(new RegrasComissao { Vendedores = new() { [99] = new RegraComissao { Valor = 1 } } });
            await acao.Should().ThrowAsync<InputModelException>().WithMessage("*99*");
        }

        [Fact]
        public async Task Extrato_do_mes_soma_por_vendedor_sem_estornadas_e_controla_o_pagamento()
        {
            _vendas.Add(Venda(1, 1, new DateTime(2026, 11, 3), 50000m, 2));
            _vendas.Add(Venda(2, 1, new DateTime(2026, 11, 30, 18, 0, 0), 70000m, 2));
            _vendas.Add(Venda(3, 2, new DateTime(2026, 11, 15), 40000m, 1));
            _vendas.Add(Venda(4, 2, new DateTime(2026, 11, 16), 90000m, 1, status: "E"));
            _vendas.Add(Venda(5, 1, new DateTime(2026, 10, 31), 30000m, 2));

            await _uc.MarcarPagasAsync(new[] { 1, 4 }, new DateTime(2026, 12, 5));
            var r = await _uc.ExtratoAsync(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), null);

            r.Vendas.Select(v => v.VenId).Should().Equal(2, 3, 1);
            r.TotalVendido.Should().Be(160000m);
            r.TotalComissao.Should().Be(2800m);
            r.TotalPago.Should().Be(1000m);
            r.TotalAPagar.Should().Be(1800m);
            var ana = r.Vendedores.Single(v => v.VendedorId == 1);
            (ana.Nome, ana.Vendas, ana.Comissao, ana.Pago, ana.APagar).Should().Be(("Ana", 2, 2400m, 1000m, 1400m));
            r.Vendas.Single(v => v.VenId == 1).PagaEm.Should().Be(new DateTime(2026, 12, 5));

            (await _uc.ExtratoAsync(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), 2)).Vendas.Should().ContainSingle();

            await _uc.MarcarPagasAsync(new[] { 1 }, null);
            (await _uc.ExtratoAsync(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), null)).TotalPago.Should().Be(0m);
        }

        [Fact]
        public void Venda_estornada_nao_tem_comissao_a_pagar()
        {
            var v = Venda(1, 1, DateTime.Today, 1000m, 1, status: "E");
            var acao = () => v.MarcarComissaoPaga(DateTime.Today);
            acao.Should().Throw<DomainException>();
        }
    }
}
