using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Financiamentos;
using ConnectVeiculos.Core.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Services.Financiamento;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Credere
{
    /// <summary>
    /// Integracao com a API da Credere (docs.meucredere.com.br/api). Pontos da API
    /// que pesam aqui: dinheiro sempre em centavos; header Store-Id em todo
    /// /banks_api; o lead (cliente) precisa existir na loja antes de simular;
    /// a simulacao e assincrona (o POST devolve o uuid e cada banco responde
    /// depois); renovar o token invalida o par antigo.
    /// </summary>
    public class CredereService : ICredereService
    {
        private const string KeyToken = "CREDERE_TOKEN";
        private const string KeyConta = "CREDERE_CONTA";
        private static readonly TimeSpan RenovarAntes = TimeSpan.FromDays(7);
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly int[] PrazosPadrao = { 24, 36, 48, 60 };

        // Renovar o token cancela o refresh_token antigo: duas renovacoes ao mesmo
        // tempo derrubariam a conexao. Uma por loja (tenant) de cada vez.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> TravasToken = new();

        private readonly CredereSettings _settings;
        private readonly HttpClient _http;
        private readonly IConfiguracaoSistemaRepository _config;
        private readonly ITokenProtector _protector;
        private readonly IOAuthStateProtector _state;
        private readonly IFinanciamentoLojaRepository _fichas;
        private readonly ITenantContext _tenant;
        private readonly ILogger<CredereService> _logger;

        public CredereService(IOptions<CredereSettings> settings, HttpClient http, IConfiguracaoSistemaRepository config,
            ITokenProtector protector, IOAuthStateProtector state, IFinanciamentoLojaRepository fichas,
            ITenantContext tenant, ILogger<CredereService> logger)
        {
            _settings = settings.Value;
            _http = http;
            _config = config;
            _protector = protector;
            _state = state;
            _fichas = fichas;
            _tenant = tenant;
            _logger = logger;
        }

        private sealed class TokenSalvo
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime ExpiraEm { get; set; }
        }

        private sealed class ContaSalva
        {
            public string? Conta { get; set; }
            public string? Usuario { get; set; }
            public string? UsuarioCpf { get; set; }
            public DateTime ConectadoEm { get; set; }
            public DateTime ExpiraEm { get; set; }
            public int? LojaId { get; set; }
            public string? LojaNome { get; set; }
        }

        // ================================================================
        // Conexao (OAuth)
        // ================================================================

        public string UrlAutorizacao()
        {
            ExigirConfigurado();
            var state = _state.Proteger(_tenant.TenantSlug ?? string.Empty);
            // A doc manda o escopo com "+" literal na URL (vira espaco ao decodificar).
            return $"{Base()}/authorize?response_type=code&scope={_settings.Escopos}" +
                   $"&client_id={Uri.EscapeDataString(_settings.ClientId!)}" +
                   $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
                   $"&state={Uri.EscapeDataString(state)}";
        }

        public async Task<CredereStatus> ConectarAsync(string code, string? state, CancellationToken ct = default)
        {
            ExigirConfigurado();
            if (string.IsNullOrWhiteSpace(code)) throw new CredereException("A Credere não devolveu o código de autorização.");
            try
            {
                _state.Validar(state, _tenant.TenantSlug ?? string.Empty);
            }
            catch (OAuthStateException ex)
            {
                throw new CredereException(ex.Message);
            }

            using var doc = await PedirTokenAsync(new Dictionary<string, object?>
            {
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["scope"] = _settings.Escopos,
                ["redirect_uri"] = _settings.RedirectUri,
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret
            }, ct);

            var token = LerToken(doc.RootElement);
            await SalvarTokenAsync(token);
            var user = doc.RootElement.TryGetProperty("user", out var u) ? u : default;
            var conta = new ContaSalva
            {
                Conta = user.ValueKind == JsonValueKind.Object && user.TryGetProperty("account", out var a) ? Texto(a, "name") : null,
                Usuario = user.ValueKind == JsonValueKind.Object ? Texto(user, "name") : null,
                UsuarioCpf = user.ValueKind == JsonValueKind.Object ? SoDigitos(Texto(user, "cpf")) : null,
                ConectadoEm = DateTime.UtcNow,
                ExpiraEm = token.ExpiraEm
            };

            // Uma loja so na conta: ja escolhe.
            var lojas = await ListarLojasAsync(token.AccessToken, ct);
            if (lojas.Count == 1)
            {
                conta.LojaId = lojas[0].Id;
                conta.LojaNome = lojas[0].Nome;
            }
            await SalvarContaAsync(conta);
            _logger.LogInformation("Credere conectada (conta {Conta}, {Lojas} loja(s))", conta.Conta, lojas.Count);
            return await StatusAsync(ct);
        }

        public async Task<CredereStatus> EscolherLojaAsync(int lojaId, CancellationToken ct = default)
        {
            var conta = await ContaAsync() ?? throw new CredereException("Conecte a Credere primeiro.");
            var token = await AccessTokenAsync(ct);
            var loja = (await ListarLojasAsync(token, ct)).FirstOrDefault(l => l.Id == lojaId)
                ?? throw new CredereException("Loja não encontrada na conta da Credere.");
            conta.LojaId = loja.Id;
            conta.LojaNome = loja.Nome;
            await SalvarContaAsync(conta);
            return await StatusAsync(ct);
        }

        public async Task DesconectarAsync(CancellationToken ct = default)
        {
            var token = await TokenAsync();
            if (token != null)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, $"{Base()}/revoke");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                    using var resp = await _http.SendAsync(req, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    // Sem revogar la, a chave expira sozinha; aqui ela some de qualquer jeito.
                    _logger.LogWarning(ex, "Credere: falha ao revogar o token");
                }
            }
            await _config.SetValorAsync(KeyToken, "");
            await _config.SetValorAsync(KeyConta, "");
        }

        public async Task<CredereStatus> StatusAsync(CancellationToken ct = default)
        {
            var status = new CredereStatus
            {
                Configurado = _settings.Configurado,
                SiteConexao = Uri.TryCreate(_settings.RedirectUri, UriKind.Absolute, out var retorno) ? retorno.GetLeftPart(UriPartial.Authority) : null
            };
            var conta = await ContaAsync();
            if (conta == null || await TokenAsync() == null) return status;

            status.Conectado = true;
            status.Conta = conta.Conta;
            status.Usuario = conta.Usuario;
            status.ConectadoEm = conta.ConectadoEm;
            status.ExpiraEm = conta.ExpiraEm;
            status.LojaId = conta.LojaId;
            status.LojaNome = conta.LojaNome;
            if (!_settings.Configurado) return status;

            try
            {
                var token = await AccessTokenAsync(ct);
                status.Lojas = await ListarLojasAsync(token, ct);
                if (conta.LojaId is { } lojaId)
                {
                    using var doc = await ChamarAsync(HttpMethod.Get, $"/stores/{lojaId}/integrated_banks", null, null, ct);
                    status.Bancos = Lista(doc!.RootElement, "banks").Select(b =>
                    {
                        var cred = b.TryGetProperty("bank_credential", out var c) && c.ValueKind == JsonValueKind.Object ? c : default;
                        return new CredereBancoLoja
                        {
                            Codigo = Texto(b, "code") ?? "",
                            Nome = Texto(b, "tradename") ?? Texto(b, "name") ?? "",
                            Ativo = cred.ValueKind == JsonValueKind.Object && cred.TryGetProperty("active", out var at) && at.ValueKind == JsonValueKind.True,
                            Status = cred.ValueKind == JsonValueKind.Object ? Texto(cred, "status") : null
                        };
                    }).ToList();
                }
            }
            catch (CredereException ex)
            {
                status.Erro = ex.Message;
            }
            return status;
        }

        // ================================================================
        // Veiculo
        // ================================================================

        public async Task<IReadOnlyList<CredereModeloVeiculo>> BuscarModelosAsync(string busca, int? ano, CancellationToken ct = default)
        {
            busca = (busca ?? "").Trim();
            if (busca.Length < 2) throw new CredereException("Digite a marca e o modelo do veículo.");
            var q = $"/vehicle_models?per_page=20&name_molicar_fipe={Uri.EscapeDataString(busca)}";
            if (ano is > 1950 and < 2100)
                q += $"&year_end_greater_than_or_equal_to={ano}&year_start_less_than_or_equal_to={ano}";
            using var doc = await ChamarAsync(HttpMethod.Get, q, null, null, ct);
            return Lista(doc!.RootElement, "vehicle_models").Select(m => new CredereModeloVeiculo
            {
                Id = Inteiro(m, "id") ?? 0,
                Marca = Texto(m, "brand") ?? "",
                Nome = Texto(m, "name") ?? "",
                Versao = Texto(m, "version"),
                AnoInicio = Inteiro(m, "year_start"),
                AnoFim = Inteiro(m, "year_end"),
                Fipe = Texto(m, "fipe_code")
            }).Where(m => m.Id > 0).ToList();
        }

        // ================================================================
        // Ficha -> lead -> simulacao
        // ================================================================

        public async Task<IReadOnlyList<CredereCampoExigido>> CamposExigidosAsync(int fichaId, int? vendedorId, CancellationToken ct = default)
        {
            var ficha = await FichaAsync(fichaId, vendedorId);
            var loja = await LojaObrigatoriaAsync();
            using var doc = await ChamarAsync(HttpMethod.Get, $"/banks_api/leads/{ficha.FifCpf}/required_fields", null, loja, ct);
            var req = doc!.RootElement.TryGetProperty("data", out var d) && d.TryGetProperty("requirements", out var r) ? r : default;
            return LerExigencias(req);
        }

        /// <summary>requirements: campo -> [codigos de banco]; address e um objeto com subcampos.</summary>
        public static List<CredereCampoExigido> LerExigencias(JsonElement req)
        {
            var lista = new List<CredereCampoExigido>();
            if (req.ValueKind != JsonValueKind.Object) return lista;
            foreach (var p in req.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Array)
                    lista.Add(new CredereCampoExigido { Campo = p.Name, Bancos = p.Value.EnumerateArray().Select(x => x.ToString()).ToList() });
                else if (p.Value.ValueKind == JsonValueKind.Object)
                    foreach (var s in p.Value.EnumerateObject().Where(s => s.Value.ValueKind == JsonValueKind.Array))
                        lista.Add(new CredereCampoExigido { Campo = $"{p.Name}.{s.Name}", Bancos = s.Value.EnumerateArray().Select(x => x.ToString()).ToList() });
            }
            return lista.Where(c => c.Bancos.Count > 0).ToList();
        }

        public async Task<CredereSimulacao> SimularAsync(int fichaId, CredereSimulacaoInput input, int? vendedorId, string? emailUsuario,
            CancellationToken ct = default)
        {
            var ficha = await FichaAsync(fichaId, vendedorId);
            var conta = await ContaAsync() ?? throw new CredereException("Conecte a Credere em Integrações.");
            var loja = conta.LojaId ?? throw new CredereException("Escolha a loja da Credere em Integrações.");
            var prazos = ValidarSimulacao(input, ficha);

            // Guarda o que o vendedor preencheu, para a proxima simulacao ja vir pronta.
            var dados = FichaFinanciamentoService.DadosCredere(ficha) ?? new CredereFichaDados();
            dados.Nascimento = input.Nascimento?.Date;
            dados.PossuiCnh = input.PossuiCnh;
            dados.Genero = string.IsNullOrWhiteSpace(input.Genero) ? null : input.Genero.Trim().ToUpperInvariant();
            dados.RendaMensal = input.RendaMensal;
            dados.Cep = string.IsNullOrWhiteSpace(input.Cep) ? null : SoDigitos(input.Cep);
            dados.Ocupacao = string.IsNullOrWhiteSpace(input.Ocupacao) ? null : input.Ocupacao.Trim();
            dados.ModeloId = input.ModeloId;
            dados.ModeloNome = input.ModeloNome;
            dados.AnoFabricacao = input.AnoFabricacao;
            dados.UfLicenciamento = input.UfLicenciamento.Trim().ToUpperInvariant();
            dados.CidadeLicenciamento = input.CidadeLicenciamento.Trim();
            dados.Prazos = prazos;

            var token = await AccessTokenAsync(ct);
            await GravarLeadAsync(ficha, dados, loja, ct);
            var vendedorCpf = await CpfVendedorAsync(loja, emailUsuario, conta, ct);

            var valor = Centavos(ficha.FifValorVeiculo);
            var entrada = Centavos(ficha.FifEntrada);
            var corpo = new Dictionary<string, object?>
            {
                ["simulation"] = new Dictionary<string, object?>
                {
                    ["process_bank_suggested_conditions"] = true,
                    ["seller_cpf"] = vendedorCpf,
                    ["retrieve_lead"] = new Dictionary<string, object?> { ["cpf_cnpj"] = CpfFormatado(ficha.FifCpf) },
                    ["assets_value"] = valor,
                    ["commercial"] = false,
                    ["vehicle"] = new Dictionary<string, object?>
                    {
                        ["credere_vehicle_model_id"] = input.ModeloId,
                        ["licensing_uf"] = dados.UfLicenciamento,
                        ["licensing_city"] = dados.CidadeLicenciamento,
                        ["manufacture_year"] = input.AnoFabricacao,
                        ["model_year"] = input.AnoModelo,
                        ["asset_value"] = valor,
                        ["zero_km"] = false
                    },
                    ["conditions"] = prazos.Select(p => new Dictionary<string, object?>
                    {
                        ["installments"] = p,
                        ["down_payment"] = entrada
                    }).ToList()
                }
            };

            using var doc = await ChamarAsync(HttpMethod.Post, "/banks_api/simulations", corpo, loja, ct);
            var simulacao = LerSimulacao(doc!.RootElement);
            if (string.IsNullOrEmpty(simulacao.Uuid)) throw new CredereException("A Credere não devolveu o número da simulação.");

            dados.UltimaSimulacao = simulacao.Uuid;
            dados.UltimaSimulacaoEm = DateTime.UtcNow;
            ficha.AtualizarCredere(JsonSerializer.Serialize(dados, Json));
            await _fichas.SalvarFichaAsync(ficha);
            _logger.LogInformation("Credere: simulacao {Uuid} da ficha {Ficha}", simulacao.Uuid, ficha.FifId);
            return simulacao;
        }

        public async Task<CredereSimulacao> ObterSimulacaoAsync(int fichaId, int? vendedorId, CancellationToken ct = default)
        {
            var ficha = await FichaAsync(fichaId, vendedorId);
            var uuid = FichaFinanciamentoService.DadosCredere(ficha)?.UltimaSimulacao
                ?? throw new CredereException("Esta ficha ainda não foi simulada na Credere.");
            var loja = await LojaObrigatoriaAsync();
            using var doc = await ChamarAsync(HttpMethod.Get, $"/banks_api/simulations/{Uri.EscapeDataString(uuid)}", null, loja, ct);
            return LerSimulacao(doc!.RootElement);
        }

        private static List<int> ValidarSimulacao(CredereSimulacaoInput input, FichaFinanciamento ficha)
        {
            if (input.ModeloId <= 0) throw new CredereException("Escolha a versão do veículo na lista da Credere.");
            if (input.AnoModelo is < 1950 or > 2100 || input.AnoFabricacao is < 1950 or > 2100 || input.AnoFabricacao > input.AnoModelo)
                throw new CredereException("Ano de fabricação/modelo inválido.");
            if (string.IsNullOrWhiteSpace(input.UfLicenciamento) || input.UfLicenciamento.Trim().Length != 2)
                throw new CredereException("Informe a UF de licenciamento.");
            if (string.IsNullOrWhiteSpace(input.CidadeLicenciamento)) throw new CredereException("Informe a cidade de licenciamento.");
            if (input.Genero is { Length: > 0 } g && g.Trim().ToUpperInvariant() is not ("F" or "M"))
                throw new CredereException("Gênero inválido.");
            if (input.RendaMensal is < 0) throw new CredereException("Renda não pode ser negativa.");
            if (input.Nascimento is { } n && (n > DateTime.Today.AddYears(-18) || n < DateTime.Today.AddYears(-110)))
                throw new CredereException("Data de nascimento inválida (o cliente precisa ser maior de idade).");
            if (ficha.FifValorVeiculo <= 0 || ficha.FifEntrada >= ficha.FifValorVeiculo)
                throw new CredereException("Confira o valor do veículo e a entrada na ficha.");

            var prazos = (input.Prazos ?? new()).Where(p => p is >= 6 and <= 72).Distinct().OrderBy(p => p).ToList();
            if (prazos.Count == 0) prazos = PrazosPadrao.ToList();
            // Cada prazo vira uma condicao por banco: poucos prazos, menos fila no banco.
            if (prazos.Count > 6) throw new CredereException("Escolha até 6 prazos por simulação.");
            return prazos;
        }

        private async Task GravarLeadAsync(FichaFinanciamento f, CredereFichaDados d, int loja, CancellationToken ct)
        {
            var lead = new Dictionary<string, object?>
            {
                ["cpf_cnpj"] = CpfFormatado(f.FifCpf),
                ["name"] = f.FifNome,
                ["phone_number"] = TelefoneFormatado(f.FifTelefone),
                ["email"] = f.FifEmail
            };
            if (d.Nascimento is { } n) lead["birthdate"] = n.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (d.PossuiCnh is { } cnh) lead["has_cnh"] = cnh;
            if (d.Genero != null) lead["retrieve_gender"] = d.Genero;
            if (d.Ocupacao != null) lead["retrieve_occupation"] = d.Ocupacao;
            if (d.RendaMensal is { } renda) lead["monthly_income"] = Centavos(renda);
            if (d.Cep is { Length: 8 } cep) lead["address"] = new Dictionary<string, object?> { ["zip_code"] = $"{cep[..5]}-{cep[5..]}" };
            var corpo = new Dictionary<string, object?> { ["lead"] = lead };

            // O mesmo CPF nao pode ser lead duas vezes na loja: atualiza se ja existe.
            using var existente = await ChamarAsync(HttpMethod.Get, $"/banks_api/leads/{f.FifCpf}", null, loja, ct, aceitar404: true);
            if (existente == null)
                using (await ChamarAsync(HttpMethod.Post, "/banks_api/leads", corpo, loja, ct)) { }
            else
                using (await ChamarAsync(HttpMethod.Patch, $"/banks_api/leads/{f.FifCpf}", corpo, loja, ct)) { }
        }

        /// <summary>
        /// A Credere exige o CPF de um usuario dela como vendedor. Procura o vendedor
        /// com o mesmo e-mail de quem esta no ConnectVeiculos; sem ele, usa quem
        /// conectou a conta.
        /// </summary>
        private async Task<string> CpfVendedorAsync(int loja, string? email, ContaSalva conta, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
                    // GET /users (Listar usuarios da doc; o antigo proposals_filter_list saiu dela).
                    using var doc = await ChamarAsync(HttpMethod.Get, $"/users?per_page=20&active=true&store_id={loja}&q={Uri.EscapeDataString(email.Trim())}", null, null, ct);
                    var raiz = doc!.RootElement;
                    // A doc mostra ora { users: [...] }, ora { user: {...} }.
                    var usuarios = Lista(raiz, "users").ToList();
                    if (raiz.TryGetProperty("user", out var um) && um.ValueKind == JsonValueKind.Object) usuarios.Add(um);
                    var achado = usuarios.FirstOrDefault(x => string.Equals(Texto(x, "email"), email.Trim(), StringComparison.OrdinalIgnoreCase));
                    var cpf = achado.ValueKind == JsonValueKind.Object ? SoDigitos(Texto(achado, "cpf")) : "";
                    if (cpf.Length == 11) return cpf;
                }
                catch (CredereException ex)
                {
                    _logger.LogInformation("Credere: lista de vendedores indisponivel ({Erro}); usando quem conectou", ex.Message);
                }
            }
            return conta.UsuarioCpf is { Length: 11 } c
                ? c
                : throw new CredereException("Não achei o vendedor na Credere. Cadastre o vendedor lá com o mesmo e-mail daqui.");
        }

        // ================================================================
        // Leitura da simulacao
        // ================================================================

        public static CredereSimulacao LerSimulacao(JsonElement raiz)
        {
            var d = raiz.TryGetProperty("data", out var data) ? data : raiz;
            var s = new CredereSimulacao
            {
                Uuid = Texto(d, "uuid") ?? "",
                CriadaEm = d.TryGetProperty("created_at", out var c) && c.ValueKind == JsonValueKind.String && c.TryGetDateTime(out var dt) ? dt : null
            };
            foreach (var x in Lista(d, "conditions"))
            {
                var banco = x.TryGetProperty("bank", out var b) && b.ValueKind == JsonValueKind.Object ? b : default;
                var task = x.TryGetProperty("process_task", out var t) && t.ValueKind == JsonValueKind.Object ? t : default;
                var processando = task.ValueKind != JsonValueKind.Object || Texto(task, "ended_at") == null;
                var sucesso = Bool(x, "success") == true;
                var disponivel = Bool(x, "available") == true;
                s.Condicoes.Add(new CredereCondicao
                {
                    Id = Longo(x, "id") ?? 0,
                    Banco = banco.ValueKind == JsonValueKind.Object ? Texto(banco, "nickname") ?? Texto(banco, "name") ?? "" : "",
                    CodigoBanco = banco.ValueKind == JsonValueKind.Object ? Texto(banco, "febraban_code") ?? "" : "",
                    Prazo = Inteiro(x, "installments") ?? 0,
                    Entrada = Reais(x, "down_payment") ?? 0,
                    Parcela = Reais(x, "first_installment_value"),
                    UltimaParcela = Reais(x, "last_installment_value"),
                    Financiado = Reais(x, "financed_amount"),
                    TotalPago = Reais(x, "amount_paid_in_financing"),
                    TaxaMensal = Numero(x, "interest_monthly"),
                    CetMensal = Numero(x, "cet_monthly"),
                    Processando = processando,
                    Disponivel = !processando && sucesso && disponivel,
                    Aprovado = Bool(x, "credit_approved") == true,
                    PreAprovacao = Inteiro(x, "pre_approval_status"),
                    Motivo = processando ? null : Texto(x, "reason"),
                    MotivoCodigo = processando ? null : Texto(x, "reason_identifier"),
                    Sugestao = Texto(x, "suggestion_reason"),
                    EntradaMinima = Reais(x, "bank_minimum_down_payment"),
                    EntradaSugerida = Reais(x, "bank_down_payment_suggestion"),
                    SubsidioFabrica = string.Equals(Texto(x, "subsidy_type"), "factory", StringComparison.OrdinalIgnoreCase)
                });
            }
            s.Concluida = s.Condicoes.Count > 0 && s.Condicoes.All(x => !x.Processando);
            // Quem financia primeiro: aprovadas/pre-aprovadas e menor parcela.
            s.Condicoes = s.Condicoes
                .OrderBy(x => x.Processando)
                .ThenByDescending(x => x.Disponivel)
                .ThenByDescending(x => x.Aprovado)
                .ThenByDescending(x => x.PreAprovacao ?? 0)
                .ThenBy(x => x.Banco)
                .ThenBy(x => x.Prazo)
                .ThenBy(x => x.Entrada)
                .ToList();
            return s;
        }

        // ================================================================
        // Token
        // ================================================================

        private async Task<string> AccessTokenAsync(CancellationToken ct)
        {
            ExigirConfigurado();
            var token = await TokenAsync() ?? throw new CredereException("Conecte a Credere em Integrações.");
            if (token.ExpiraEm - DateTime.UtcNow > RenovarAntes) return token.AccessToken;

            var trava = TravasToken.GetOrAdd(_tenant.TenantSlug ?? "", _ => new SemaphoreSlim(1, 1));
            await trava.WaitAsync(ct);
            try
            {
                // Reler dentro da trava: outra requisicao pode ter acabado de renovar.
                token = await TokenAsync() ?? throw new CredereException("Conecte a Credere em Integrações.");
                if (token.ExpiraEm - DateTime.UtcNow > RenovarAntes) return token.AccessToken;

                using var doc = await PedirTokenAsync(new Dictionary<string, object?>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = token.RefreshToken,
                    ["client_id"] = _settings.ClientId,
                    ["client_secret"] = _settings.ClientSecret
                }, ct);
                var novo = LerToken(doc.RootElement);
                await SalvarTokenAsync(novo);
                if (await ContaAsync() is { } conta)
                {
                    conta.ExpiraEm = novo.ExpiraEm;
                    await SalvarContaAsync(conta);
                }
                _logger.LogInformation("Credere: token renovado (expira em {Expira:yyyy-MM-dd})", novo.ExpiraEm);
                return novo.AccessToken;
            }
            finally
            {
                trava.Release();
            }
        }

        private async Task<JsonDocument> PedirTokenAsync(Dictionary<string, object?> corpo, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{Base()}/token")
            {
                Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json")
            };
            HttpResponseMessage resp;
            try { resp = await _http.SendAsync(req, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new CredereException("Não foi possível falar com a Credere agora. Tente de novo em instantes.");
            }
            using (resp)
            {
                var texto = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Credere: token recusado ({Status})", (int)resp.StatusCode);
                    throw new CredereException(resp.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                        ? "A Credere recusou a autorização. Conecte de novo em Integrações."
                        : MensagemDeErro(texto, (int)resp.StatusCode));
                }
                return JsonDocument.Parse(texto);
            }
        }

        private static TokenSalvo LerToken(JsonElement r)
        {
            var access = Texto(r, "access_token");
            var refresh = Texto(r, "refresh_token");
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh))
                throw new CredereException("A Credere não devolveu a chave de acesso.");
            var criado = Longo(r, "created_at") is { } epoch ? DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime : DateTime.UtcNow;
            var segundos = Longo(r, "expires_in") ?? 15552000;
            return new TokenSalvo { AccessToken = access, RefreshToken = refresh, ExpiraEm = criado.AddSeconds(segundos) };
        }

        private async Task<TokenSalvo?> TokenAsync()
        {
            var cifrado = await _config.GetValorAsync(KeyToken);
            if (string.IsNullOrWhiteSpace(cifrado)) return null;
            try { return JsonSerializer.Deserialize<TokenSalvo>(_protector.Unprotect(cifrado), Json); }
            catch (Exception ex) when (ex is JsonException or System.Security.Cryptography.CryptographicException)
            {
                _logger.LogWarning(ex, "Credere: token guardado ilegivel");
                return null;
            }
        }

        private Task SalvarTokenAsync(TokenSalvo t)
            => _config.SetValorAsync(KeyToken, _protector.Protect(JsonSerializer.Serialize(t, Json)));

        private async Task<ContaSalva?> ContaAsync()
        {
            var json = await _config.GetValorAsync(KeyConta);
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<ContaSalva>(json, Json); }
            catch (JsonException) { return null; }
        }

        private Task SalvarContaAsync(ContaSalva c) => _config.SetValorAsync(KeyConta, JsonSerializer.Serialize(c, Json));

        // ================================================================
        // HTTP
        // ================================================================

        private async Task<List<CredereLoja>> ListarLojasAsync(string token, CancellationToken ct)
        {
            using var doc = await EnviarAsync(HttpMethod.Get, "/stores?per_page=20", null, null, token, aceitar404: false, ct);
            return Lista(doc!.RootElement, "stores").Select(l => new CredereLoja
            {
                Id = Inteiro(l, "id") ?? 0,
                Nome = Texto(l, "display_name") ?? Texto(l, "name") ?? "",
                Cnpj = Texto(l, "cnpj"),
                Cidade = Texto(l, "city"),
                Uf = Texto(l, "uf")
            }).Where(l => l.Id > 0).ToList();
        }

        private async Task<JsonDocument?> ChamarAsync(HttpMethod metodo, string caminho, object? corpo, int? loja, CancellationToken ct,
            bool aceitar404 = false)
            => await EnviarAsync(metodo, caminho, corpo, loja, await AccessTokenAsync(ct), aceitar404, ct);

        private async Task<JsonDocument?> EnviarAsync(HttpMethod metodo, string caminho, object? corpo, int? loja, string token,
            bool aceitar404, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(metodo, Base() + caminho);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (loja is { } id) req.Headers.Add("Store-Id", id.ToString(CultureInfo.InvariantCulture));
            if (corpo != null) req.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try { resp = await _http.SendAsync(req, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Credere: falha de comunicacao em {Metodo} {Caminho}", metodo, Rota(caminho));
                throw new CredereException("Não foi possível falar com a Credere agora. Tente de novo em instantes.");
            }
            using (resp)
            {
                var texto = await resp.Content.ReadAsStringAsync(ct);
                if (resp.StatusCode == HttpStatusCode.NotFound && aceitar404) return null;
                if (resp.IsSuccessStatusCode)
                    return string.IsNullOrWhiteSpace(texto) ? JsonDocument.Parse("{}") : JsonDocument.Parse(texto);

                _logger.LogWarning("Credere: {Metodo} {Caminho} respondeu {Status}", metodo, Rota(caminho), (int)resp.StatusCode);
                throw resp.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => new CredereException("A conexão com a Credere expirou ou foi revogada. Conecte de novo em Integrações."),
                    HttpStatusCode.TooManyRequests => new CredereException(
                        "Limite de simulações da Credere atingido" +
                        (resp.Headers.RetryAfter?.Delta is { } espera ? $". Tente de novo em {Math.Ceiling(espera.TotalMinutes)} min." : ". Tente mais tarde.")),
                    _ => new CredereException(MensagemDeErro(texto, (int)resp.StatusCode))
                };
            }
        }

        /// <summary>A Credere responde { error: { message, details } }; message ja vem em portugues.</summary>
        public static string MensagemDeErro(string? corpo, int status)
        {
            try
            {
                using var doc = JsonDocument.Parse(corpo ?? "");
                var r = doc.RootElement;
                if (r.TryGetProperty("error", out var e))
                {
                    if (e.ValueKind == JsonValueKind.Object && Texto(e, "message") is { Length: > 0 } m) return $"Credere: {m}";
                    if (e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } s)
                        return $"Credere: {(Texto(r, "error_description") ?? s)}";
                }
            }
            catch (JsonException) { /* corpo vazio ou HTML */ }
            return status >= 500
                ? "A Credere está instável agora. Tente de novo em alguns minutos."
                : $"A Credere recusou o pedido ({status}).";
        }

        /// <summary>Caminho sem CPF nem query, para o log.</summary>
        private static string Rota(string caminho)
            => System.Text.RegularExpressions.Regex.Replace(caminho.Split('?')[0], @"\d{11,14}", "{doc}");

        // ================================================================
        // Auxiliares
        // ================================================================

        private string Base() => _settings.BaseUrl.TrimEnd('/');

        private void ExigirConfigurado()
        {
            if (!_settings.Configurado)
                throw new CredereException("A integração com a Credere ainda não foi liberada para o ConnectVeiculos.");
        }

        private async Task<int> LojaObrigatoriaAsync()
            => (await ContaAsync())?.LojaId ?? throw new CredereException("Conecte a Credere e escolha a loja em Integrações.");

        private async Task<FichaFinanciamento> FichaAsync(int fichaId, int? vendedorId)
        {
            var ficha = await _fichas.ObterFichaAsync(fichaId);
            if (ficha == null || (vendedorId.HasValue && ficha.R_UsuId != vendedorId.Value))
                throw new CredereException("Ficha não encontrada.");
            return ficha;
        }

        public static long Centavos(decimal reais) => (long)Math.Round(reais * 100m, MidpointRounding.AwayFromZero);

        private static IEnumerable<JsonElement> Lista(JsonElement e, string nome)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var a) && a.ValueKind == JsonValueKind.Array
                ? a.EnumerateArray()
                : Enumerable.Empty<JsonElement>();

        private static string? Texto(JsonElement e, string nome)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v)
                ? v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString(),
                    JsonValueKind.Number => v.GetRawText(),
                    _ => null
                }
                : null;

        private static long? Longo(JsonElement e, string nome)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(nome, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
            if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            return null;
        }

        private static int? Inteiro(JsonElement e, string nome) => Longo(e, nome) is { } n ? (int)n : null;

        private static decimal? Numero(JsonElement e, string nome)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(nome, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
            if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return d;
            return null;
        }

        private static decimal? Reais(JsonElement e, string nome) => Longo(e, nome) is { } c ? c / 100m : null;

        private static bool? Bool(JsonElement e, string nome)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v)
                ? v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
                : null;

        private static string SoDigitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

        private static string CpfFormatado(string cpf)
        {
            var d = SoDigitos(cpf);
            return d.Length == 11 ? $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}" : d;
        }

        private static string TelefoneFormatado(string tel)
        {
            var d = SoDigitos(tel);
            return d.Length switch
            {
                11 => $"({d[..2]}) {d[2..7]}-{d[7..]}",
                10 => $"({d[..2]}) {d[2..6]}-{d[6..]}",
                _ => d
            };
        }
    }
}
