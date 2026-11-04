using System.Text.Json;
using ConnectVeiculos.Core.Entities.Financiamentos;
using ConnectVeiculos.Core.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Services.Credere;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Teste real contra a Credere, para validar a integracao quando as chaves
    /// de parceiro chegarem. Depende de rede e de uma chave de acesso tirada com
    /// scripts/credere-token.sh, por isso so roda com CREDERE_TESTE_REAL=1:
    ///
    ///   CREDERE_TESTE_REAL=1 dotnet test ConnectVeiculos.Tests --filter CredereRealTests
    ///
    /// Sem mais nada, so le: conta, lojas, bancos da loja e versoes de veiculo.
    /// A simulacao vai aos bancos de verdade (pre-analise do CPF), por isso so
    /// acontece com CREDERE_TESTE_SIMULAR=1 e CREDERE_CPF_TESTE (use o CPF de
    /// teste que a Credere indicar no sandbox).
    ///
    /// Outras variaveis (todas opcionais): CREDERE_TOKEN_ARQUIVO (padrao
    /// ~/.credere-token.json), CREDERE_BASE_URL (sandbox), CREDERE_LOJA_ID,
    /// CREDERE_BUSCA_VEICULO, CREDERE_ANO, CREDERE_VALOR, CREDERE_ENTRADA,
    /// CREDERE_VENDEDOR_EMAIL, CREDERE_UF, CREDERE_CIDADE.
    /// </summary>
    public class CredereRealTests
    {
        private readonly ITestOutputHelper _saida;
        public CredereRealTests(ITestOutputHelper saida) => _saida = saida;

        private sealed class Config : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> V = new();
            public Task<string> GetValorAsync(string c) => Task.FromResult(V.TryGetValue(c, out var v) ? v : null!);
            public Task SetValorAsync(string c, string v) { V[c] = v; return Task.CompletedTask; }
        }

        private sealed class SemCifra : ITokenProtector
        {
            public string Protect(string s) => s;
            public string Unprotect(string s) => s;
        }

        private sealed class Fichas : IFinanciamentoLojaRepository
        {
            public readonly List<FichaFinanciamento> Itens = new();
            public Task<IReadOnlyList<BancoFinanciamentoLoja>> ListarBancosAsync() => Task.FromResult<IReadOnlyList<BancoFinanciamentoLoja>>(new List<BancoFinanciamentoLoja>());
            public Task<BancoFinanciamentoLoja?> ObterBancoAsync(int id) => Task.FromResult<BancoFinanciamentoLoja?>(null);
            public Task SalvarBancoAsync(BancoFinanciamentoLoja banco) => Task.CompletedTask;
            public Task RemoverBancoAsync(BancoFinanciamentoLoja banco) => Task.CompletedTask;
            public Task<IReadOnlyList<FichaFinanciamento>> ListarFichasAsync(int? v) => Task.FromResult<IReadOnlyList<FichaFinanciamento>>(Itens);
            public Task<FichaFinanciamento?> ObterFichaAsync(int id) => Task.FromResult(Itens.FirstOrDefault(f => f.FifId == id));
            public Task SalvarFichaAsync(FichaFinanciamento f)
            {
                if (f.FifId == 0)
                {
                    typeof(FichaFinanciamento).GetProperty(nameof(FichaFinanciamento.FifId))!.SetValue(f, Itens.Count + 1);
                    Itens.Add(f);
                }
                return Task.CompletedTask;
            }
        }

        private static string? Env(string nome) => Environment.GetEnvironmentVariable(nome) is { Length: > 0 } v ? v.Trim() : null;

        [Fact]
        public async Task Fluxo_Real_Na_Credere()
        {
            if (Env("CREDERE_TESTE_REAL") != "1") return;

            var arquivo = Env("CREDERE_TOKEN_ARQUIVO")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".credere-token.json");
            File.Exists(arquivo).Should().BeTrue($"rode antes: bash scripts/credere-token.sh (procurei {arquivo})");
            using var token = JsonDocument.Parse(await File.ReadAllTextAsync(arquivo));
            var t = token.RootElement;

            var config = new Config();
            var expira = DateTimeOffset.FromUnixTimeSeconds(t.GetProperty("created_at").GetInt64() + t.GetProperty("expires_in").GetInt64()).UtcDateTime;
            config.V["CREDERE_TOKEN"] = JsonSerializer.Serialize(new
            {
                accessToken = t.GetProperty("access_token").GetString(),
                refreshToken = t.GetProperty("refresh_token").GetString(),
                // So o teste: sem renovar (renovar invalidaria a chave do arquivo).
                expiraEm = expira > DateTime.UtcNow.AddDays(8) ? expira : DateTime.UtcNow.AddDays(30)
            });
            var cpfConta = t.TryGetProperty("user", out var u) && u.TryGetProperty("cpf", out var c) ? new string((c.GetString() ?? "").Where(char.IsDigit).ToArray()) : null;
            config.V["CREDERE_CONTA"] = JsonSerializer.Serialize(new { conta = "teste", usuarioCpf = cpfConta, conectadoEm = DateTime.UtcNow, expiraEm = expira });

            var settings = new CredereSettings
            {
                // As chaves de parceiro nao sao usadas sem renovar; so precisam existir.
                ClientId = Env("CREDERE_CLIENT_ID") ?? "teste",
                ClientSecret = Env("CREDERE_CLIENT_SECRET") ?? "teste"
            };
            if (Env("CREDERE_BASE_URL") is { } baseUrl) settings.BaseUrl = baseUrl;

            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(x => x.TenantSlug).Returns("teste-real");
            var fichas = new Fichas();
            var service = new CredereService(Options.Create(settings), new HttpClient { Timeout = TimeSpan.FromSeconds(60) }, config,
                new SemCifra(), new Mock<IOAuthStateProtector>().Object, fichas, tenant.Object, NullLogger<CredereService>.Instance);

            // 1. Conta e lojas
            var status = await service.StatusAsync();
            status.Conectado.Should().BeTrue();
            status.Erro.Should().BeNull();
            _saida.WriteLine($"Lojas na conta: {status.Lojas.Count}");
            foreach (var l in status.Lojas) _saida.WriteLine($"  {l.Id} - {l.Nome} ({l.Cnpj}) {l.Cidade}/{l.Uf}");
            status.Lojas.Should().NotBeEmpty("a conta precisa ter ao menos uma loja");

            var lojaId = int.TryParse(Env("CREDERE_LOJA_ID"), out var id) ? id : status.Lojas[0].Id;
            status = await service.EscolherLojaAsync(lojaId);
            _saida.WriteLine($"Loja escolhida: {status.LojaNome} ({lojaId})");
            _saida.WriteLine($"Bancos da loja: {status.Bancos.Count}");
            foreach (var b in status.Bancos) _saida.WriteLine($"  {b.Codigo} {b.Nome}: {(b.Ativo ? "ativo" : "desativado")}, {b.Status}");

            // 2. Versoes do veiculo
            var ano = int.TryParse(Env("CREDERE_ANO"), out var a) ? a : DateTime.Today.Year - 4;
            var modelos = await service.BuscarModelosAsync(Env("CREDERE_BUSCA_VEICULO") ?? "COROLLA", ano);
            _saida.WriteLine($"Versões encontradas para {ano}: {modelos.Count}");
            foreach (var m in modelos.Take(5)) _saida.WriteLine($"  {m.Id} {m.Marca} {m.Nome} {m.Versao} ({m.AnoInicio}-{m.AnoFim}) FIPE {m.Fipe}");

            // 3. Simulacao (so com autorizacao explicita: vai aos bancos)
            if (Env("CREDERE_TESTE_SIMULAR") != "1" || Env("CREDERE_CPF_TESTE") is not { } cpf)
            {
                _saida.WriteLine("Simulação não executada (ligue CREDERE_TESTE_SIMULAR=1 e informe CREDERE_CPF_TESTE).");
                return;
            }
            modelos.Should().NotBeEmpty("preciso de uma versão de veículo para simular");

            var ficha = new FichaFinanciamento(1, DateTime.UtcNow);
            ficha.AtualizarCliente("Cliente Teste da Silva", new string(cpf.Where(char.IsDigit).ToArray()), "11999990000", "cliente.teste@exemplo.com");
            var valor = decimal.TryParse(Env("CREDERE_VALOR"), out var vv) ? vv : 80000m;
            var entrada = decimal.TryParse(Env("CREDERE_ENTRADA"), out var ee) ? ee : valor * 0.3m;
            ficha.AtualizarNegocio(null, valor, entrada, (short)ano, null);
            await fichas.SalvarFichaAsync(ficha);

            var campos = await service.CamposExigidosAsync(ficha.FifId, null);
            _saida.WriteLine("Campos exigidos pelos bancos da loja:");
            foreach (var cmp in campos) _saida.WriteLine($"  {cmp.Campo}: {string.Join(", ", cmp.Bancos)}");

            var sim = await service.SimularAsync(ficha.FifId, new CredereSimulacaoInput
            {
                Nascimento = new DateTime(1990, 1, 15), PossuiCnh = true, Genero = "M", RendaMensal = 8000m, Cep = "13320000", Ocupacao = "1",
                ModeloId = modelos[0].Id, ModeloNome = modelos[0].Versao, AnoFabricacao = ano, AnoModelo = ano,
                UfLicenciamento = Env("CREDERE_UF") ?? "SP", CidadeLicenciamento = Env("CREDERE_CIDADE") ?? "Salto",
                Prazos = new() { 36, 48 }
            }, null, Env("CREDERE_VENDEDOR_EMAIL"));
            _saida.WriteLine($"Simulação {sim.Uuid} criada; aguardando os bancos...");

            for (var i = 0; i < 36 && !sim.Concluida; i++)
            {
                await Task.Delay(5000);
                sim = await service.ObterSimulacaoAsync(ficha.FifId, null);
            }
            _saida.WriteLine($"Concluída: {sim.Concluida}. Condições: {sim.Condicoes.Count}");
            foreach (var x in sim.Condicoes)
                _saida.WriteLine($"  {x.Banco} {x.Prazo}x entrada {x.Entrada:N2} parcela {x.Parcela:N2} taxa {x.TaxaMensal}% " +
                                 $"pré {x.PreAprovacao} {(x.Disponivel ? "DISPONÍVEL" : "indisponível")} {x.Sugestao} {x.Motivo}");
            sim.Condicoes.Should().NotBeEmpty();
        }
    }
}
