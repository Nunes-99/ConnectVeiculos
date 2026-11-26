using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.UseCases.Estoque;
using ConnectVeiculos.Core.Entities.Despesas;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Despesas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases
{
    public class EstoqueParadoTests
    {
        private static readonly EstoqueParadoConfig Padrao = new();

        // ---------------- Regra da sugestao ----------------

        [Fact]
        public void Carro_novo_no_estoque_com_preco_normal_nao_muda()
        {
            var r = SugestaoPreco.Calcular(50000m, 40000m, 50000m, 10, Padrao);

            r.Faixa.Should().Be(FaixaEstoque.Normal);
            r.Mudar.Should().BeFalse();
            r.PrecoSugerido.Should().Be(50000m);
            r.MargemAtual.Should().Be(20m);
            r.DiferencaFipe.Should().Be(0m);
            r.PrecoMinimo.Should().Be(42000m, "custo 40.000 + 5%");
        }

        [Fact]
        public void Carro_novo_muito_acima_da_fipe_sugere_5_por_cento_acima()
        {
            var r = SugestaoPreco.Calcular(60000m, 40000m, 50000m, 10, Padrao);

            r.Mudar.Should().BeTrue();
            r.PrecoSugerido.Should().Be(52500m);
            r.Motivo.Should().Contain("20% acima da FIPE");
        }

        [Fact]
        public void Preco_abaixo_do_custo_mais_margem_sugere_subir()
        {
            var r = SugestaoPreco.Calcular(41000m, 40000m, 50000m, 10, Padrao);

            r.PrecoSugerido.Should().Be(42000m);
            r.Motivo.Should().Contain("abaixo do custo");
        }

        [Fact]
        public void Atencao_baixa_3_por_cento_em_centenas()
        {
            var r = SugestaoPreco.Calcular(49990m, 40000m, 52000m, 50, Padrao);

            r.Faixa.Should().Be(FaixaEstoque.Atencao);
            r.PrecoSugerido.Should().Be(48400m, "49.990 x 0,97 = 48.490,30, para baixo em centenas");
            r.Motivo.Should().Contain("50 dias");
        }

        [Fact]
        public void Atencao_acima_da_fipe_vai_para_a_fipe()
        {
            var r = SugestaoPreco.Calcular(55000m, 40000m, 50000m, 60, Padrao);
            r.PrecoSugerido.Should().Be(50000m);
        }

        [Fact]
        public void Critico_baixa_6_por_cento_e_ate_3_abaixo_da_fipe()
        {
            SugestaoPreco.Calcular(50000m, 30000m, 60000m, 120, Padrao).PrecoSugerido.Should().Be(47000m);
            SugestaoPreco.Calcular(50000m, 30000m, 50000m, 120, Padrao).PrecoSugerido.Should().Be(47000m);
            SugestaoPreco.Calcular(52000m, 30000m, 50000m, 120, Padrao).PrecoSugerido.Should().Be(48500m, "FIPE 50.000 - 3%");
        }

        [Fact]
        public void Nunca_abaixo_do_custo_mais_margem_minima()
        {
            var r = SugestaoPreco.Calcular(44000m, 41000m, 45000m, 120, Padrao);

            r.PrecoSugerido.Should().Be(43100m, "41.000 + 5% = 43.050, para cima em centenas");
        }

        [Fact]
        public void No_limite_da_margem_nao_sugere_baixar_e_explica()
        {
            var r = SugestaoPreco.Calcular(42000m, 40000m, 45000m, 120, Padrao);

            r.Mudar.Should().BeFalse();
            r.PrecoSugerido.Should().Be(42000m);
            r.Motivo.Should().Contain("limite da margem mínima");
        }

        [Fact]
        public void Sem_custo_e_sem_fipe_usa_so_o_tempo()
        {
            var r = SugestaoPreco.Calcular(30000m, 0m, null, 100, Padrao);

            r.PrecoSugerido.Should().Be(28200m);
            r.MargemAtual.Should().BeNull();
            r.DiferencaFipe.Should().BeNull();
            r.PrecoMinimo.Should().Be(0m);
        }

        [Fact]
        public void Faixas_seguem_a_configuracao_da_loja()
        {
            var c = new EstoqueParadoConfig { DiasAtencao = 30, DiasCritico = 60 };
            SugestaoPreco.Faixa(29, c).Should().Be(FaixaEstoque.Normal);
            SugestaoPreco.Faixa(30, c).Should().Be(FaixaEstoque.Atencao);
            SugestaoPreco.Faixa(60, c).Should().Be(FaixaEstoque.Critico);
        }

        // ---------------- Consulta ----------------

        private sealed class ConfigEmMemoria : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> Valores = new();
            public Task<string> GetValorAsync(string chave) => Task.FromResult(Valores.TryGetValue(chave, out var v) ? v : null!);
            public Task SetValorAsync(string chave, string valor) { Valores[chave] = valor; return Task.CompletedTask; }
        }

        private static readonly DateTime Hoje = new(2026, 11, 26);

        private static Veiculo Carro(int id, int dias, decimal preco, decimal compra, decimal? fipe, string status = "D", int loja = 1) =>
            new(id, loja, 1, "VW - VolksWagen", "GOL 1.0", 2020, $"ABC1D{id:00}", "", "Prata", 40000, preco,
                Hoje.AddDays(-dias), status, "A", compra, veiPrecoFipe: fipe);

        private (EstoqueParadoUseCase UseCase, ConfigEmMemoria Config) Criar(params Veiculo[] carros)
        {
            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetAllAsync()).ReturnsAsync(carros);
            veiculos.Setup(v => v.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => carros.FirstOrDefault(c => c.VeiId == id)!);
            var despesas = new Mock<IVeiculoDespesaRepository>();
            var lista = new[] { new VeiculoDespesa(1, 2, "Manutencao", "Pneus", 1500m, Hoje), new VeiculoDespesa(2, 2, "Estetica", "Polimento", 500m, Hoje) };
            despesas.Setup(d => d.GetAllAsync()).ReturnsAsync(lista);
            despesas.Setup(d => d.GetByVeiculoIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => lista.Where(d => d.R_VeiId == id));
            var lojas = new Mock<ILojaRepository>();
            var loja = new Loja(1, "Matriz", "", "", "", "", "", "", "", "", "", "", "", "", "", "", true);
            lojas.Setup(l => l.GetAllAsync()).ReturnsAsync(new[] { loja });
            lojas.Setup(l => l.GetByIdAsync(1)).ReturnsAsync(loja);
            var config = new ConfigEmMemoria();
            return (new EstoqueParadoUseCase(veiculos.Object, despesas.Object, lojas.Object, config) { Hoje = () => Hoje }, config);
        }

        [Fact]
        public async Task Lista_so_os_disponiveis_parados_do_mais_antigo_com_despesas_no_custo()
        {
            var (uc, _) = Criar(
                Carro(1, 10, 50000m, 40000m, 50000m),
                Carro(2, 50, 50000m, 40000m, 52000m),
                Carro(3, 120, 50000m, 30000m, 60000m),
                Carro(4, 200, 50000m, 30000m, 60000m, status: "V"));

            var r = await uc.ConsultarAsync();

            r.Disponiveis.Should().Be(3);
            r.Atencao.Should().Be(1);
            r.Critico.Should().Be(1);
            r.MediaDias.Should().Be(60);
            r.Veiculos.Select(v => v.VeiId).Should().Equal(3, 2);
            var dois = r.Veiculos[1];
            dois.Despesas.Should().Be(2000m);
            dois.Custo.Should().Be(42000m);
            dois.Loja.Should().Be("Matriz");
            dois.Descricao.Should().Be("VolksWagen GOL 1.0 2020");
            r.CustoParado.Should().Be(72000m);
        }

        [Fact]
        public async Task Sugestao_de_um_veiculo_e_config_salva()
        {
            var (uc, config) = Criar(Carro(1, 35, 50000m, 40000m, 50000m));

            (await uc.SugestaoAsync(1))!.Faixa.Should().Be("normal");
            await uc.SalvarConfigAsync(new EstoqueParadoConfig { DiasAtencao = 30, DiasCritico = 60, MargemMinima = 8 });
            config.Valores[EstoqueParadoUseCase.ChaveMargemMinima].Should().Be("8");

            var item = (await uc.SugestaoAsync(1))!;
            item.Faixa.Should().Be("atencao");
            item.PrecoMinimo.Should().Be(43200m);
            (await uc.SugestaoAsync(99)).Should().BeNull();
        }

        [Theory]
        [InlineData(0, 90, 5)]
        [InlineData(60, 60, 5)]
        [InlineData(45, 90, 120)]
        public async Task Config_invalida_e_recusada(int atencao, int critico, decimal margem)
        {
            var (uc, _) = Criar();
            var acao = () => uc.SalvarConfigAsync(new EstoqueParadoConfig { DiasAtencao = atencao, DiasCritico = critico, MargemMinima = margem });
            await acao.Should().ThrowAsync<InputModelException>();
        }
    }
}
