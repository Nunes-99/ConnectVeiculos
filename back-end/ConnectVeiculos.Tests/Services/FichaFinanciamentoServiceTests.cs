using ConnectVeiculos.Core.Entities.Financiamentos;
using ConnectVeiculos.Core.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Infrastructure.Services.Financiamento;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    public class FichaFinanciamentoServiceTests
    {
        private sealed class Repo : IFinanciamentoLojaRepository
        {
            public readonly List<BancoFinanciamentoLoja> Bancos = new();
            public readonly List<FichaFinanciamento> Fichas = new();

            public Task<IReadOnlyList<BancoFinanciamentoLoja>> ListarBancosAsync() => Task.FromResult<IReadOnlyList<BancoFinanciamentoLoja>>(Bancos.ToList());
            public Task<BancoFinanciamentoLoja?> ObterBancoAsync(int id) => Task.FromResult(Bancos.FirstOrDefault(b => b.BflId == id));
            public Task SalvarBancoAsync(BancoFinanciamentoLoja banco)
            {
                if (banco.BflId == 0)
                {
                    typeof(BancoFinanciamentoLoja).GetProperty(nameof(BancoFinanciamentoLoja.BflId))!.SetValue(banco, Bancos.Count + 1);
                    Bancos.Add(banco);
                }
                return Task.CompletedTask;
            }
            public Task RemoverBancoAsync(BancoFinanciamentoLoja banco) { Bancos.Remove(banco); return Task.CompletedTask; }

            public Task<IReadOnlyList<FichaFinanciamento>> ListarFichasAsync(int? vendedorId)
                => Task.FromResult<IReadOnlyList<FichaFinanciamento>>(Fichas.Where(f => vendedorId == null || f.R_UsuId == vendedorId).ToList());
            public Task<FichaFinanciamento?> ObterFichaAsync(int id) => Task.FromResult(Fichas.FirstOrDefault(f => f.FifId == id));
            public Task SalvarFichaAsync(FichaFinanciamento ficha)
            {
                if (ficha.FifId == 0)
                {
                    typeof(FichaFinanciamento).GetProperty(nameof(FichaFinanciamento.FifId))!.SetValue(ficha, Fichas.Count + 1);
                    Fichas.Add(ficha);
                }
                return Task.CompletedTask;
            }
        }

        private readonly Repo _repo = new();
        private readonly FichaFinanciamentoService _service;

        public FichaFinanciamentoServiceTests()
        {
            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetByIdAsync(42)).ReturnsAsync(NotasFiscais.NfeVeiculoBuilderTests.Carro());
            _service = new FichaFinanciamentoService(_repo, veiculos.Object);
        }

        private static List<FaixaTaxaBanco> Tabela() => new()
        {
            new() { AnoDe = 2015, EntradaMinimaPct = 20, Taxas = { new() { Meses = 48, TaxaMensal = 1.8m }, new() { Meses = 24, TaxaMensal = 1.6m } } },
            new() { AnoDe = 2015, EntradaMinimaPct = 40, Taxas = { new() { Meses = 48, TaxaMensal = 1.6m } } },
            new() { AnoDe = 2010, AnoAte = 2014, EntradaMinimaPct = 30, Taxas = { new() { Meses = 36, TaxaMensal = 2.3m } } }
        };

        private static FichaInput Ficha() => new()
        {
            Nome = "  Maria   da Silva ", Cpf = "529.982.247-25", Telefone = "(11) 99999-0000", Email = "Maria@Exemplo.com",
            VeiculoId = 42, Entrada = 30000m, Consentimento = true
        };

        // ---------------- Calculo ----------------

        [Fact]
        public void Parcela_Price_Bate_Com_A_Formula()
        {
            CalculoFinanciamento.Parcela(10000m, 0.02m, 12).Should().BeApproximately(945.60m, 0.01m);
            CalculoFinanciamento.Parcela(1200m, 0m, 12).Should().Be(100m);
        }

        [Fact]
        public void Iof_Soma_Adicional_E_Diario_Ate_365_Dias()
        {
            // Sem juros: 12 amortizacoes iguais, dias 30..360 (soma 2340).
            CalculoFinanciamento.Iof(10000m, 0m, 12).Should().BeApproximately(38m + 10000m / 12 * 0.000082m * 2340, 0.01m);
            // Acima de 365 dias o diario para de crescer: 0,38% + no maximo 3%.
            CalculoFinanciamento.Iof(10000m, 0m, 60).Should().BeLessThan(10000m * (0.0038m + 0.000082m * 365));
        }

        [Fact]
        public void Estimativa_Financia_Iof_E_Tarifas_E_Cet_Fica_Acima_Da_Taxa()
        {
            var e = CalculoFinanciamento.Estimar(40000m, 1.99m, 48, tarifas: 1200m, tarifasFinanciadas: true);

            e.Iof.Should().BeInRange(152m, 1400m);
            e.ValorFinanciado.Should().Be(Math.Round(40000m + 1200m + e.Iof, 2));
            e.ValorParcela.Should().BeApproximately(CalculoFinanciamento.Parcela(e.ValorFinanciado, 0.0199m, 48), 0.01m);
            e.CetMensal.Should().BeGreaterThan(1.99m);
            e.CetAnual.Should().BeGreaterThan(e.CetMensal * 12);
            e.TotalPago.Should().Be(Math.Round(e.ValorParcela * 48, 2));
        }

        [Fact]
        public void Tarifa_A_Vista_Nao_Entra_No_Financiado_Mas_Entra_No_Total()
        {
            var e = CalculoFinanciamento.Estimar(40000m, 1.99m, 48, tarifas: 1200m, tarifasFinanciadas: false);
            e.ValorFinanciado.Should().Be(Math.Round(40000m + e.Iof, 2));
            e.TotalPago.Should().BeApproximately(e.ValorParcela * 48 + 1200m, 0.01m);
        }

        [Theory]
        [InlineData(2018, 45, 1.6)]
        [InlineData(2018, 25, 1.8)]
        [InlineData(2012, 30, 2.3)]
        public void Escolhe_A_Faixa_Pelo_Ano_E_Pela_Entrada(short ano, int entradaPct, decimal taxa48ou36)
        {
            var (faixa, motivo) = CalculoFinanciamento.EscolherFaixa(Tabela(), ano, 100000m, entradaPct * 1000m);
            motivo.Should().BeNull();
            faixa!.Taxas.Max(t => t.Meses).Should().BeOneOf(48, 36);
            faixa.Taxas.First(t => t.Meses == faixa.Taxas.Max(x => x.Meses)).TaxaMensal.Should().Be(taxa48ou36);
        }

        [Fact]
        public void Sem_Faixa_Explica_O_Motivo()
        {
            CalculoFinanciamento.EscolherFaixa(Tabela(), 2008, 100000m, 50000m).Motivo.Should().Contain("2008");
            CalculoFinanciamento.EscolherFaixa(Tabela(), 2020, 100000m, 10000m).Motivo.Should().Contain("20%").And.Contain("20.000,00");
        }

        // ---------------- Bancos e estimativa ----------------

        [Fact]
        public async Task Estimativa_Traz_Todos_Os_Bancos_Do_Menor_Custo_Para_O_Maior()
        {
            await _service.SalvarBancoAsync(new BancoLojaInfo { Nome = "Banco Caro", Faixas = Tabela(), TarifaCadastro = 900m, RegistroContrato = 300m });
            await _service.SalvarBancoAsync(new BancoLojaInfo
            {
                Nome = "Banco Barato", PortalUrl = "https://portal.exemplo.com.br",
                Faixas = new() { new() { EntradaMinimaPct = 10, Taxas = { new() { Meses = 48, TaxaMensal = 1.3m } } } }
            });
            await _service.SalvarBancoAsync(new BancoLojaInfo { Nome = "So Antigos", Faixas = new() { new() { AnoAte = 2010, Taxas = { new() { Meses = 24, TaxaMensal = 2m } } } } });
            await _service.SalvarBancoAsync(new BancoLojaInfo { Nome = "Desligado", Ativo = false, Faixas = Tabela() });

            var r = await _service.EstimarAsync(new EstimativaInput { ValorVeiculo = 90000m, Entrada = 30000m, AnoVeiculo = 2020 });

            r.Select(b => b.Banco).Should().Equal("Banco Barato", "Banco Caro", "So Antigos");
            r[0].PortalUrl.Should().Be("https://portal.exemplo.com.br");
            r[1].Prazos.Select(p => p.Meses).Should().Equal(24, 48);
            r[1].Prazos.Should().OnlyContain(p => p.Tarifas == 1200m);
            r[2].Prazos.Should().BeEmpty();
            r[2].Motivo.Should().Contain("2020");
        }

        [Fact]
        public async Task Banco_Valida_Portal_Taxa_E_Prazo()
        {
            await FluentActions.Awaiting(() => _service.SalvarBancoAsync(new BancoLojaInfo { Nome = "X", PortalUrl = "http://inseguro" }))
                .Should().ThrowAsync<FinanciamentoException>();
            await FluentActions.Awaiting(() => _service.SalvarBancoAsync(new BancoLojaInfo { Nome = "Banco", PortalUrl = "javascript:alert(1)" }))
                .Should().ThrowAsync<FinanciamentoException>();
            await FluentActions.Awaiting(() => _service.SalvarBancoAsync(new BancoLojaInfo
            {
                Nome = "Banco", Faixas = new() { new() { Taxas = { new() { Meses = 120, TaxaMensal = 1m } } } }
            })).Should().ThrowAsync<FinanciamentoException>().WithMessage("*72*");
        }

        [Fact]
        public async Task Sem_Banco_Cadastrado_Pede_Para_Cadastrar()
        {
            await FluentActions.Awaiting(() => _service.EstimarAsync(new EstimativaInput { ValorVeiculo = 50000m, Entrada = 10000m, AnoVeiculo = 2020 }))
                .Should().ThrowAsync<FinanciamentoException>().WithMessage("*Cadastre os bancos*");
        }

        // ---------------- Fichas ----------------

        [Fact]
        public async Task Ficha_Exige_Autorizacao_Do_Cliente()
        {
            var sem = Ficha();
            sem.Consentimento = false;
            await FluentActions.Awaiting(() => _service.CriarFichaAsync(sem, 7))
                .Should().ThrowAsync<FinanciamentoException>().WithMessage("*autorização*");
            _repo.Fichas.Should().BeEmpty();
        }

        [Fact]
        public async Task Ficha_Normaliza_Os_Dados_E_Puxa_Valor_E_Ano_Do_Veiculo()
        {
            var f = await _service.CriarFichaAsync(Ficha(), 7);

            f.Nome.Should().Be("Maria da Silva");
            f.Cpf.Should().Be("52998224725");
            f.Telefone.Should().Be("11999990000");
            f.Email.Should().Be("maria@exemplo.com");
            f.ValorVeiculo.Should().Be(NotasFiscais.NfeVeiculoBuilderTests.Carro().VeiPreco);
            f.AnoVeiculo.Should().Be(NotasFiscais.NfeVeiculoBuilderTests.Carro().VeiAno);
            f.Veiculo.Should().Contain("Corolla");
            f.Status.Should().Be(FichaFinanciamento.Aberta);
            f.VendedorId.Should().Be(7);
        }

        [Theory]
        [InlineData("Maria", "52998224725", "11999990000", "m@x.com", "nome completo")]
        [InlineData("Maria Silva", "11111111111", "11999990000", "m@x.com", "CPF")]
        [InlineData("Maria Silva", "52998224725", "9999", "m@x.com", "Telefone")]
        [InlineData("Maria Silva", "52998224725", "11999990000", "sem-arroba", "E-mail")]
        public async Task Ficha_Recusa_Dado_Invalido(string nome, string cpf, string tel, string email, string campo)
        {
            var d = Ficha();
            d.Nome = nome; d.Cpf = cpf; d.Telefone = tel; d.Email = email;
            await FluentActions.Awaiting(() => _service.CriarFichaAsync(d, 7))
                .Should().ThrowAsync<FinanciamentoException>().WithMessage($"*{campo}*");
        }

        [Fact]
        public async Task Vendedor_So_Ve_A_Propria_Ficha()
        {
            var f = await _service.CriarFichaAsync(Ficha(), 7);

            (await _service.ListarFichasAsync(8)).Should().BeEmpty();
            (await _service.ListarFichasAsync(null)).Should().HaveCount(1);
            await FluentActions.Awaiting(() => _service.ObterFichaAsync(f.Id, 8))
                .Should().ThrowAsync<FinanciamentoException>().WithMessage("*não encontrada*");
            await FluentActions.Awaiting(() => _service.AlterarStatusAsync(f.Id, "PERDIDA", 8))
                .Should().ThrowAsync<FinanciamentoException>();
            (await _service.ObterFichaAsync(f.Id, 7)).Id.Should().Be(f.Id);
        }

        [Fact]
        public async Task Proposta_Aprovada_Marca_A_Ficha_E_Substitui_A_Anterior_Do_Mesmo_Banco()
        {
            var banco = await _service.SalvarBancoAsync(new BancoLojaInfo { Nome = "Banco A", Faixas = Tabela() });
            var f = await _service.CriarFichaAsync(Ficha(), 7);

            await _service.RegistrarPropostaAsync(f.Id, new PropostaBanco { BancoId = banco.Id, Status = "em_analise" }, 7);
            var r = await _service.RegistrarPropostaAsync(f.Id, new PropostaBanco
            {
                BancoId = banco.Id, Status = "APROVADA", Meses = 48, TaxaMensal = 1.79m, ValorParcela = 1650m, Banco = "nome ignorado"
            }, 7);

            r.Status.Should().Be(FichaFinanciamento.Aprovada);
            r.Propostas.Should().ContainSingle();
            r.Propostas[0].Banco.Should().Be("Banco A");
            r.Propostas[0].ValorParcela.Should().Be(1650m);

            r = await _service.RemoverPropostaAsync(f.Id, banco.Id, 7);
            r.Propostas.Should().BeEmpty();
        }

        [Fact]
        public async Task Proposta_Recusa_Situacao_E_Banco_Inexistentes()
        {
            var banco = await _service.SalvarBancoAsync(new BancoLojaInfo { Nome = "Banco A" });
            var f = await _service.CriarFichaAsync(Ficha(), 7);
            await FluentActions.Awaiting(() => _service.RegistrarPropostaAsync(f.Id, new PropostaBanco { BancoId = banco.Id, Status = "TALVEZ" }, 7))
                .Should().ThrowAsync<FinanciamentoException>();
            await FluentActions.Awaiting(() => _service.RegistrarPropostaAsync(f.Id, new PropostaBanco { BancoId = 99, Status = "ENVIADA" }, 7))
                .Should().ThrowAsync<FinanciamentoException>().WithMessage("*Banco*");
        }
    }
}
