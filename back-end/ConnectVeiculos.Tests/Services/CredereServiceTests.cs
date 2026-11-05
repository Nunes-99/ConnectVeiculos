using System.Net;
using System.Text;
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

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Credere de mentira respondendo como a documentacao (docs.meucredere.com.br/api):
    /// confere o que mandamos (centavos, Store-Id, formatos de CPF/telefone) e
    /// como lemos a resposta.
    /// </summary>
    public class CredereServiceTests
    {
        private sealed class Config : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> V = new();
            public Task<string> GetValorAsync(string c) => Task.FromResult(V.TryGetValue(c, out var v) ? v : null!);
            public Task SetValorAsync(string c, string v) { V[c] = v; return Task.CompletedTask; }
        }

        private sealed class Protetor : ITokenProtector
        {
            public string Protect(string s) => "cifrado:" + s;
            public string Unprotect(string s) => s.StartsWith("cifrado:") ? s[8..] : throw new System.Security.Cryptography.CryptographicException();
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
            public Task SalvarFichaAsync(FichaFinanciamento ficha)
            {
                if (ficha.FifId == 0)
                {
                    typeof(FichaFinanciamento).GetProperty(nameof(FichaFinanciamento.FifId))!.SetValue(ficha, Itens.Count + 1);
                    Itens.Add(ficha);
                }
                return Task.CompletedTask;
            }
        }

        private sealed record Pedido(string Metodo, string Caminho, string? StoreId, string? Autorizacao, JsonElement? Corpo);

        /// <summary>Responde por "METODO /caminho" (sem query); grava cada pedido.</summary>
        private sealed class CredereFalsa : HttpMessageHandler
        {
            public readonly List<Pedido> Pedidos = new();
            public readonly Dictionary<string, Func<HttpResponseMessage>> Rotas = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            {
                var caminho = r.RequestUri!.AbsolutePath.Replace("/api/v1", "");
                JsonElement? corpo = null;
                if (r.Content != null) corpo = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)).RootElement.Clone();
                Pedidos.Add(new Pedido(r.Method.Method, caminho,
                    r.Headers.TryGetValues("Store-Id", out var s) ? s.First() : null,
                    r.Headers.Authorization?.Parameter, corpo));
                return Rotas.TryGetValue($"{r.Method.Method} {caminho}", out var f)
                    ? f()
                    : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":{\"message\":\"rota de teste ausente\"}}") };
            }
        }

        private static HttpResponseMessage Json(object corpo, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json") };

        private readonly Config _config = new();
        private readonly Fichas _fichas = new();
        private readonly CredereFalsa _api = new();
        private readonly Mock<IOAuthStateProtector> _state = new();
        private CredereSettings _settings = new() { ClientId = "app-cv", ClientSecret = "segredo-cv" };

        private CredereService Servico() => new(Options.Create(_settings), new HttpClient(_api), _config, new Protetor(),
            _state.Object, _fichas, Tenant(), NullLogger<CredereService>.Instance);

        private static ITenantContext Tenant()
        {
            var t = new Mock<ITenantContext>();
            t.SetupGet(x => x.TenantSlug).Returns("loja-teste");
            return t.Object;
        }

        private void Conectado(DateTime? expira = null, int? loja = 77)
        {
            _config.V["CREDERE_TOKEN"] = "cifrado:" + JsonSerializer.Serialize(new
            {
                accessToken = "tk-atual", refreshToken = "rf-atual", expiraEm = expira ?? DateTime.UtcNow.AddDays(90)
            });
            _config.V["CREDERE_CONTA"] = JsonSerializer.Serialize(new
            {
                conta = "Diamante", usuario = "Dono", usuarioCpf = "11144477735", conectadoEm = DateTime.UtcNow, lojaId = loja, lojaNome = "Diamante Salto"
            });
        }

        private async Task<FichaFinanciamento> FichaAsync(int vendedor = 7)
        {
            var f = new FichaFinanciamento(vendedor, DateTime.UtcNow);
            f.AtualizarCliente("Maria da Silva", "52998224725", "11999990000", "maria@exemplo.com");
            f.AtualizarNegocio(42, 90000.50m, 30000m, 2020, null);
            await _fichas.SalvarFichaAsync(f);
            return f;
        }

        private static CredereSimulacaoInput Entrada() => new()
        {
            Nascimento = new DateTime(1990, 10, 30), PossuiCnh = true, Genero = "f", RendaMensal = 9000m, Cep = "13320-000",
            ModeloId = 13656, ModeloNome = "ETIOS X 1.3", AnoFabricacao = 2019, AnoModelo = 2020,
            UfLicenciamento = "sp", CidadeLicenciamento = "Salto", Prazos = new() { 48, 24, 24, 100 }
        };

        private static object Simulacao(bool processando) => new
        {
            data = new
            {
                uuid = "31a3e1a8-a318-4ca1-9a2d-8193252c1243",
                created_at = "2026-11-02T10:00:00Z",
                conditions = new object[]
                {
                    new
                    {
                        id = 1, installments = 48, down_payment = 3000000, first_installment_value = 195050, last_installment_value = 195050,
                        financed_amount = 6100000, amount_paid_in_financing = 9362400, interest_monthly = 1.79, cet_monthly = 2.31,
                        success = true, available = true, pre_approval_status = 3, reason = (string?)null, suggestion_reason = (string?)null,
                        bank = new { febraban_code = "033", nickname = "Santander", name = "Banco Santander (Brasil) S.A." },
                        process_task = new { ended_at = "2026-11-02T10:00:05Z" }
                    },
                    new
                    {
                        id = 2, installments = 48, down_payment = 3000000, success = false, available = false,
                        bank = new { febraban_code = "623", nickname = "Pan" },
                        process_task = processando ? null : new { ended_at = "2026-11-02T10:00:09Z" },
                        reason = "Campos obrigatórios em falta para o banco.", reason_identifier = "simulation_required_fields"
                    },
                    new
                    {
                        id = 3, installments = 24, down_payment = 4500000, first_installment_value = 230000, success = true, available = true,
                        pre_approval_status = 2, suggestion_reason = "bank_suggested_down_payment", subsidy_type = "factory",
                        bank_minimum_down_payment = 3500000, bank_down_payment_suggestion = 4500000,
                        bank = new { febraban_code = "341", nickname = "Itaú" },
                        process_task = new { ended_at = "2026-11-02T10:00:07Z" }
                    }
                }
            }
        };

        // ---------------- Simulacao ----------------

        [Fact]
        public async Task Simular_Cria_O_Lead_E_Manda_Tudo_Em_Centavos_Com_Store_Id()
        {
            Conectado();
            var ficha = await FichaAsync();
            _api.Rotas["POST /banks_api/leads"] = () => Json(new { data = new { id = 1 } }, HttpStatusCode.Created);
            _api.Rotas["GET /users"] = () => Json(new { users = new[] { new { email = "MARIA.VENDEDORA@loja.com", cpf = "390.533.447-05" } } });
            _api.Rotas["POST /banks_api/simulations"] = () => Json(Simulacao(processando: true), HttpStatusCode.Created);

            var r = await Servico().SimularAsync(ficha.FifId, Entrada(), null, "maria.vendedora@loja.com");

            // Lead: nao existia (404) -> criado, com os formatos da doc.
            _api.Pedidos.Select(p => $"{p.Metodo} {p.Caminho}").Should().ContainInOrder(
                "GET /banks_api/leads/52998224725", "POST /banks_api/leads", "POST /banks_api/simulations");
            var lead = _api.Pedidos.Single(p => p.Caminho == "/banks_api/leads").Corpo!.Value.GetProperty("lead");
            lead.GetProperty("cpf_cnpj").GetString().Should().Be("529.982.247-25");
            lead.GetProperty("phone_number").GetString().Should().Be("(11) 99999-0000");
            lead.GetProperty("birthdate").GetString().Should().Be("1990-10-30");
            lead.GetProperty("monthly_income").GetInt64().Should().Be(900000);
            lead.GetProperty("retrieve_gender").GetString().Should().Be("F");
            lead.GetProperty("has_cnh").GetBoolean().Should().BeTrue();
            lead.GetProperty("address").GetProperty("zip_code").GetString().Should().Be("13320-000");

            var sim = _api.Pedidos.Single(p => p.Caminho == "/banks_api/simulations").Corpo!.Value.GetProperty("simulation");
            sim.GetProperty("seller_cpf").GetString().Should().Be("39053344705");
            sim.GetProperty("retrieve_lead").GetProperty("cpf_cnpj").GetString().Should().Be("529.982.247-25");
            sim.GetProperty("assets_value").GetInt64().Should().Be(9000050);
            sim.GetProperty("vehicle").GetProperty("credere_vehicle_model_id").GetInt32().Should().Be(13656);
            sim.GetProperty("vehicle").GetProperty("licensing_uf").GetString().Should().Be("SP");
            sim.GetProperty("vehicle").GetProperty("zero_km").GetBoolean().Should().BeFalse();
            var condicoes = sim.GetProperty("conditions").EnumerateArray().ToList();
            condicoes.Select(c => c.GetProperty("installments").GetInt32()).Should().Equal(24, 48);
            condicoes.Should().OnlyContain(c => c.GetProperty("down_payment").GetInt64() == 3000000);

            _api.Pedidos.Where(p => p.Caminho.StartsWith("/banks_api")).Should().OnlyContain(p => p.StoreId == "77");
            _api.Pedidos.Should().OnlyContain(p => p.Autorizacao == "tk-atual");

            r.Uuid.Should().Be("31a3e1a8-a318-4ca1-9a2d-8193252c1243");
            r.Concluida.Should().BeFalse();
            var dados = Infrastructure.Services.Financiamento.FichaFinanciamentoService.DadosCredere(ficha)!;
            dados.UltimaSimulacao.Should().Be(r.Uuid);
            dados.Prazos.Should().Equal(24, 48);
            dados.UfLicenciamento.Should().Be("SP");
        }

        [Fact]
        public async Task Lead_Que_Ja_Existe_E_Atualizado_E_Sem_Vendedor_Usa_Quem_Conectou()
        {
            Conectado();
            var ficha = await FichaAsync();
            _api.Rotas["GET /banks_api/leads/52998224725"] = () => Json(new { data = new { id = 9 } });
            _api.Rotas["PATCH /banks_api/leads/52998224725"] = () => Json(new { data = new { id = 9 } });
            _api.Rotas["GET /users"] = () => Json(new { user = new { email = "outro@loja.com", cpf = "000.000.000-00" } });
            _api.Rotas["POST /banks_api/simulations"] = () => Json(Simulacao(false), HttpStatusCode.Created);

            await Servico().SimularAsync(ficha.FifId, Entrada(), null, "maria@loja.com");

            _api.Pedidos.Should().Contain(p => p.Metodo == "PATCH");
            _api.Pedidos.Should().NotContain(p => p.Metodo == "POST" && p.Caminho == "/banks_api/leads");
            _api.Pedidos.Single(p => p.Caminho == "/banks_api/simulations").Corpo!.Value
                .GetProperty("simulation").GetProperty("seller_cpf").GetString().Should().Be("11144477735");
        }

        [Fact]
        public void Le_A_Simulacao_Em_Reais_Com_Pre_Aprovacao_E_Sugestoes()
        {
            var s = CredereService.LerSimulacao(JsonDocument.Parse(JsonSerializer.Serialize(Simulacao(processando: true))).RootElement);

            s.Concluida.Should().BeFalse();
            s.Condicoes.Should().HaveCount(3);
            s.Condicoes.Last().Processando.Should().BeTrue();
            s.Condicoes.Last().Banco.Should().Be("Pan");
            s.Condicoes.Last().Motivo.Should().BeNull();

            var santander = s.Condicoes.Single(c => c.CodigoBanco == "033");
            santander.Disponivel.Should().BeTrue();
            santander.Parcela.Should().Be(1950.50m);
            santander.Entrada.Should().Be(30000m);
            santander.Financiado.Should().Be(61000m);
            santander.TaxaMensal.Should().Be(1.79m);
            santander.PreAprovacao.Should().Be(3);

            var itau = s.Condicoes.Single(c => c.CodigoBanco == "341");
            itau.Sugestao.Should().Be("bank_suggested_down_payment");
            itau.SubsidioFabrica.Should().BeTrue();
            itau.EntradaMinima.Should().Be(35000m);

            var pronta = CredereService.LerSimulacao(JsonDocument.Parse(JsonSerializer.Serialize(Simulacao(false))).RootElement);
            pronta.Concluida.Should().BeTrue();
            var pan = pronta.Condicoes.Single(c => c.CodigoBanco == "623");
            pan.Disponivel.Should().BeFalse();
            pan.MotivoCodigo.Should().Be("simulation_required_fields");
            pronta.Condicoes.First().CodigoBanco.Should().Be("033", "pre-aprovada vem antes da neutra e da indisponivel");
        }

        [Fact]
        public async Task Simulacao_Valida_Antes_De_Chamar_A_Credere()
        {
            Conectado();
            var ficha = await FichaAsync();
            var semModelo = Entrada(); semModelo.ModeloId = 0;
            var menor = Entrada(); menor.Nascimento = DateTime.Today.AddYears(-16);
            var semCidade = Entrada(); semCidade.CidadeLicenciamento = " ";

            foreach (var e in new[] { semModelo, menor, semCidade })
                await FluentActions.Awaiting(() => Servico().SimularAsync(ficha.FifId, e, null, null)).Should().ThrowAsync<CredereException>();
            _api.Pedidos.Should().BeEmpty();
        }

        [Fact]
        public async Task Vendedor_Nao_Simula_Ficha_De_Outro()
        {
            Conectado();
            var ficha = await FichaAsync(vendedor: 7);
            await FluentActions.Awaiting(() => Servico().SimularAsync(ficha.FifId, Entrada(), 8, null))
                .Should().ThrowAsync<CredereException>().WithMessage("*não encontrada*");
            await FluentActions.Awaiting(() => Servico().ObterSimulacaoAsync(ficha.FifId, 8))
                .Should().ThrowAsync<CredereException>();
            _api.Pedidos.Should().BeEmpty();
        }

        [Fact]
        public async Task Sem_Loja_Escolhida_Ou_Sem_Chaves_Do_Parceiro_Explica()
        {
            Conectado(loja: null);
            var ficha = await FichaAsync();
            await FluentActions.Awaiting(() => Servico().SimularAsync(ficha.FifId, Entrada(), null, null))
                .Should().ThrowAsync<CredereException>().WithMessage("*Escolha a loja*");

            _settings = new CredereSettings();
            FluentActions.Invoking(() => Servico().UrlAutorizacao()).Should().Throw<CredereException>().WithMessage("*ainda não foi liberada*");
            (await Servico().StatusAsync()).Configurado.Should().BeFalse();
        }

        // ---------------- Token ----------------

        [Fact]
        public async Task Token_Perto_De_Vencer_E_Renovado_Antes_E_O_Novo_Par_Fica_Salvo()
        {
            Conectado(expira: DateTime.UtcNow.AddDays(3));
            var ficha = await FichaAsync();
            _api.Rotas["POST /token"] = () => Json(new
            {
                access_token = "tk-novo", refresh_token = "rf-novo", expires_in = 15552000,
                created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            _api.Rotas["GET /banks_api/simulations/31a3"] = () => Json(Simulacao(false));
            ficha.AtualizarCredere(JsonSerializer.Serialize(new CredereFichaDados { UltimaSimulacao = "31a3" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

            await Servico().ObterSimulacaoAsync(ficha.FifId, null);

            var refresh = _api.Pedidos.Single(p => p.Caminho == "/token").Corpo!.Value;
            refresh.GetProperty("grant_type").GetString().Should().Be("refresh_token");
            refresh.GetProperty("refresh_token").GetString().Should().Be("rf-atual");
            refresh.GetProperty("client_secret").GetString().Should().Be("segredo-cv");
            _api.Pedidos.Last().Autorizacao.Should().Be("tk-novo");
            _config.V["CREDERE_TOKEN"].Should().Contain("rf-novo").And.NotContain("rf-atual");
        }

        [Fact]
        public async Task Conectar_Valida_O_State_Troca_O_Codigo_E_Escolhe_A_Unica_Loja()
        {
            _api.Rotas["GET /stores"] = () => Json(new { stores = new[] { new { id = 77, name = "Diamante", display_name = "Diamante Salto", cnpj = "00.000.000/0001-00" } } });
            _api.Rotas["GET /stores/77/integrated_banks"] = () => Json(new
            {
                banks = new[]
                {
                    new { code = "422", name = "Banco Safra S.A.", tradename = "Safra", bank_credential = new { active = true, status = "okay" } },
                    new { code = "341", name = "Itaú Unibanco S.A.", tradename = "Itaú", bank_credential = new { active = true, status = "unauthorized" } }
                }
            });

            _api.Rotas["POST /token"] = () => Json(new
            {
                access_token = "tk", refresh_token = "rf", expires_in = 15552000, created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                user = new { name = "Dono", cpf = "111.444.777-35", account = new { name = "Diamante Veículos" } }
            });

            var st = await Servico().ConectarAsync("codigo-5min", "state-ok");

            _state.Verify(s => s.Validar("state-ok", "loja-teste"), Times.Once);
            var troca = _api.Pedidos.First(p => p.Caminho == "/token").Corpo!.Value;
            troca.GetProperty("grant_type").GetString().Should().Be("authorization_code");
            troca.GetProperty("code").GetString().Should().Be("codigo-5min");
            troca.GetProperty("redirect_uri").GetString().Should().Be(_settings.RedirectUri);

            st.Conectado.Should().BeTrue();
            st.Conta.Should().Be("Diamante Veículos");
            st.LojaId.Should().Be(77);
            st.Bancos.Should().HaveCount(2);
            st.Bancos.Single(b => b.Codigo == "341").Status.Should().Be("unauthorized");
            _config.V["CREDERE_TOKEN"].Should().StartWith("cifrado:");
            _config.V["CREDERE_CONTA"].Should().NotContain("tk");
        }

        [Fact]
        public async Task State_Invalido_Nao_Troca_O_Codigo()
        {
            _state.Setup(s => s.Validar(It.IsAny<string?>(), It.IsAny<string>())).Throws(new OAuthStateException("Esta autorizacao nao foi iniciada neste navegador."));
            await FluentActions.Awaiting(() => Servico().ConectarAsync("codigo", "forjado"))
                .Should().ThrowAsync<CredereException>().WithMessage("*navegador*");
            _api.Pedidos.Should().BeEmpty();
            _config.V.Should().NotContainKey("CREDERE_TOKEN");
        }

        [Fact]
        public void Url_De_Autorizacao_Leva_Escopos_Redirect_E_State()
        {
            _state.Setup(s => s.Proteger("loja-teste")).Returns("st/+=");
            var url = Servico().UrlAutorizacao();
            url.Should().StartWith("https://app.meucredere.com.br/api/v1/authorize?response_type=code&scope=simulator+proposals");
            url.Should().Contain("client_id=app-cv").And.Contain("state=st%2F%2B%3D")
                .And.Contain("redirect_uri=https%3A%2F%2Fconnectveiculos.dev.br%2Fintegracoes%2Fcredere");
        }

        // ---------------- Erros ----------------

        [Fact]
        public async Task Erros_Da_Credere_Viram_Mensagem_Legivel()
        {
            Conectado();
            var ficha = await FichaAsync();
            ficha.AtualizarCredere("{\"ultimaSimulacao\":\"abc\"}");

            _api.Rotas["GET /banks_api/simulations/abc"] = () => new HttpResponseMessage(HttpStatusCode.Unauthorized);
            await FluentActions.Awaiting(() => Servico().ObterSimulacaoAsync(ficha.FifId, null))
                .Should().ThrowAsync<CredereException>().WithMessage("*Conecte de novo*");

            _api.Rotas["GET /banks_api/simulations/abc"] = () =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(600));
                return r;
            };
            await FluentActions.Awaiting(() => Servico().ObterSimulacaoAsync(ficha.FifId, null))
                .Should().ThrowAsync<CredereException>().WithMessage("*10 min*");

            CredereService.MensagemDeErro("{\"error\":{\"class\":\"ModelValidationError\",\"message\":\"Nome é muito longo\",\"status\":422}}", 422)
                .Should().Be("Credere: Nome é muito longo");
            CredereService.MensagemDeErro("<html>", 503).Should().Contain("instável");
            CredereService.MensagemDeErro("", 400).Should().Contain("400");
        }

        [Fact]
        public void Campos_Exigidos_Leem_Endereco_Aninhado()
        {
            var req = JsonDocument.Parse("""
                { "address": { "zip_code": ["623"] }, "birthdate": ["655","623"], "name": [], "has_cnh": ["341"] }
                """).RootElement;
            var campos = CredereService.LerExigencias(req);
            campos.Select(c => c.Campo).Should().BeEquivalentTo("address.zip_code", "birthdate", "has_cnh");
            campos.Single(c => c.Campo == "birthdate").Bancos.Should().Equal("655", "623");
        }

        [Fact]
        public void Centavos_Arredonda_Para_Cima_No_Meio()
        {
            CredereService.Centavos(90000.50m).Should().Be(9000050);
            CredereService.Centavos(0.005m).Should().Be(1);
        }
    }
}
