using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Tenants;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Pagamentos
{
    /// <summary>
    /// Assinaturas do Mercado Pago (/preapproval) sem plano associado: cada
    /// clique em "Assinar" cria uma assinatura pendente com o valor do plano e
    /// a referencia da loja; o cliente cadastra o cartao na pagina deles
    /// (init_point) e o Mercado Pago cobra todo mes sozinho. O plano so muda
    /// quando a assinatura relida na API deles esta "authorized".
    /// </summary>
    public class MercadoPagoAssinaturaService : IAssinaturaService
    {
        private const string Api = "https://api.mercadopago.com";
        public const int DiasPorPix = 30;

        private readonly HttpClient _http;
        private readonly MercadoPagoSettings _settings;
        private readonly ITenantStore _tenants;
        private readonly IPlanoRepository _planos;
        private readonly ILogger<MercadoPagoAssinaturaService> _logger;

        public MercadoPagoAssinaturaService(
            HttpClient http,
            IOptions<MercadoPagoSettings> settings,
            ITenantStore tenants,
            IPlanoRepository planos,
            ILogger<MercadoPagoAssinaturaService> logger)
        {
            _http = http;
            _settings = settings.Value;
            _tenants = tenants;
            _planos = planos;
            _logger = logger;
        }

        public bool Configurado => !string.IsNullOrWhiteSpace(_settings.AccessToken);

        public async Task<string> IniciarAsync(int tenantId, int planoId, string emailPagador, CancellationToken ct = default)
        {
            if (!Configurado)
                throw new AssinaturaException("O pagamento pelo cartão ainda não está disponível. Fale com o suporte.");

            var tenant = await _tenants.GetByIdAsync(tenantId, ct)
                ?? throw new AssinaturaException("Loja não encontrada.");
            var plano = await _planos.GetByIdAsync(planoId, ct);
            if (plano == null || !plano.PlaAtivo || plano.PlaPreco <= 0)
                throw new AssinaturaException("Plano inválido.");
            if (tenant.TenAssinaturaStatus == StatusAssinatura.Ativa && tenant.TenAssinaturaPlaId == planoId)
                throw new AssinaturaException($"Sua loja já assina o plano {plano.PlaNome}.");

            var email = emailPagador?.Trim() ?? "";
            if (!MailAddress.TryCreate(email, out _))
                throw new AssinaturaException("Informe um e-mail válido para o pagamento.");

            var site = (_settings.PublicSiteUrl ?? "").Trim().TrimEnd('/');
            var corpo = new
            {
                reason = $"ConnectVeiculos - Plano {plano.PlaNome}",
                external_reference = ReferenciaExterna(tenantId, planoId),
                payer_email = email,
                back_url = $"{site}/plano?assinatura=retorno",
                status = StatusAssinatura.Pendente,
                auto_recurring = new
                {
                    frequency = 1,
                    frequency_type = "months",
                    transaction_amount = plano.PlaPreco,
                    currency_id = "BRL"
                }
            };

            using var req = Requisicao(HttpMethod.Post, "/preapproval");
            req.Content = JsonContent.Create(corpo);
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mercado Pago recusou criar assinatura da loja {Tenant} no plano {Plano}: HTTP {Status} {Body}",
                    tenantId, plano.PlaNome, (int)resp.StatusCode, body);
                throw new AssinaturaException(MensagemDoErro(body));
            }

            using var doc = JsonDocument.Parse(body);
            var link = Texto(doc.RootElement, "init_point");
            if (string.IsNullOrEmpty(link))
                throw new AssinaturaException("O Mercado Pago não devolveu a página de pagamento. Tente de novo.");

            _logger.LogInformation("Assinatura {Id} criada (pendente) para a loja {Tenant}, plano {Plano}",
                Texto(doc.RootElement, "id"), tenantId, plano.PlaNome);
            return link;
        }

        public async Task<PixGerado> GerarPixAsync(int tenantId, int planoId, string emailPagador, CancellationToken ct = default)
        {
            if (!Configurado)
                throw new AssinaturaException("O pagamento por Pix ainda não está disponível. Fale com o suporte.");

            var tenant = await _tenants.GetByIdAsync(tenantId, ct)
                ?? throw new AssinaturaException("Loja não encontrada.");
            var plano = await _planos.GetByIdAsync(planoId, ct);
            if (plano == null || !plano.PlaAtivo || plano.PlaPreco <= 0)
                throw new AssinaturaException("Plano inválido.");
            // Pix por cima do cartao cobraria duas vezes no mesmo mes.
            if (tenant.TenAssinaturaStatus == StatusAssinatura.Ativa)
                throw new AssinaturaException("Sua loja já paga pelo cartão. Para usar Pix, cancele a assinatura do cartão primeiro.");

            var email = emailPagador?.Trim() ?? "";
            if (!MailAddress.TryCreate(email, out _))
                throw new AssinaturaException("Informe um e-mail válido para o pagamento.");

            var site = (_settings.PublicSiteUrl ?? "").Trim().TrimEnd('/');
            var expira = DateTime.UtcNow.AddHours(24);
            var corpo = new
            {
                transaction_amount = plano.PlaPreco,
                description = $"ConnectVeiculos - Plano {plano.PlaNome} ({DiasPorPix} dias)",
                payment_method_id = "pix",
                payer = new { email },
                external_reference = ReferenciaPix(tenantId, planoId),
                notification_url = $"{site}/api/assinatura/webhook/mercadopago",
                date_of_expiration = expira.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)
            };

            using var req = Requisicao(HttpMethod.Post, "/v1/payments");
            req.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
            req.Content = JsonContent.Create(corpo);
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mercado Pago recusou gerar Pix da loja {Tenant} no plano {Plano}: HTTP {Status} {Body}",
                    tenantId, plano.PlaNome, (int)resp.StatusCode, body);
                throw new AssinaturaException("O Mercado Pago não gerou o Pix. Tente de novo em instantes.");
            }

            using var doc = JsonDocument.Parse(body);
            var dados = doc.RootElement.TryGetProperty("point_of_interaction", out var poi)
                        && poi.TryGetProperty("transaction_data", out var td) ? td : default;
            var codigo = dados.ValueKind == JsonValueKind.Object ? Texto(dados, "qr_code") : null;
            if (string.IsNullOrEmpty(codigo))
                throw new AssinaturaException("O Mercado Pago não devolveu o QR Code. Tente de novo.");

            _logger.LogInformation("Pix {Id} gerado para a loja {Tenant}, plano {Plano}", Texto(doc.RootElement, "id"), tenantId, plano.PlaNome);
            return new PixGerado
            {
                Codigo = codigo,
                QrCodeBase64 = Texto(dados, "qr_code_base64") ?? "",
                Valor = plano.PlaPreco,
                ExpiraEm = expira
            };
        }

        public async Task<int> EncerrarPeriodosPixVencidosAsync(CancellationToken ct = default)
        {
            var free = await PlanoGratuitoAsync(ct);
            var agora = DateTime.UtcNow;
            var encerradas = 0;
            foreach (var t in await _tenants.ListActiveAsync(ct))
            {
                if (!PeriodoPixVenceu(t, agora)) continue;
                await _tenants.AtualizarAsync(t.TenId, x =>
                {
                    x.EncerrarPeriodoPix();
                    if (free.HasValue) x.AlterarPlano(free.Value);
                }, ct);
                encerradas++;
                _logger.LogInformation("Periodo pago por Pix da loja {Tenant} acabou: plano voltou ao gratuito", t.TenSlug);
            }
            return encerradas;
        }

        /// <summary>Pix vencido e sem cartao ativo segurando o plano.</summary>
        public static bool PeriodoPixVenceu(Tenant t, DateTime agora)
            => t.TenPlanoPagoAte.HasValue && t.TenPlanoPagoAte.Value <= agora
               && t.TenAssinaturaStatus != StatusAssinatura.Ativa;

        public async Task CancelarAsync(int tenantId, CancellationToken ct = default)
        {
            var tenant = await _tenants.GetByIdAsync(tenantId, ct)
                ?? throw new AssinaturaException("Loja não encontrada.");
            if (string.IsNullOrEmpty(tenant.TenAssinaturaId) || tenant.TenAssinaturaStatus != StatusAssinatura.Ativa)
                throw new AssinaturaException("A loja não tem assinatura ativa.");

            if (!await MudarStatusNoMercadoPagoAsync(tenant.TenAssinaturaId, StatusAssinatura.Cancelada, ct))
                throw new AssinaturaException("Não consegui cancelar no Mercado Pago. Tente de novo em instantes.");

            var free = await PlanoGratuitoAsync(ct);
            var id = tenant.TenAssinaturaId;
            await _tenants.AtualizarAsync(tenantId, t =>
            {
                t.RegistrarAssinatura(id, StatusAssinatura.Cancelada, t.TenAssinaturaPlaId);
                if (free.HasValue) t.AlterarPlano(free.Value);
            }, ct);
            _logger.LogInformation("Assinatura {Id} da loja {Tenant} cancelada pelo painel", id, tenantId);
        }

        public async Task ProcessarAvisoAsync(string tipo, string id, CancellationToken ct = default)
        {
            if (!Configurado || string.IsNullOrWhiteSpace(id)) return;

            if (tipo == "payment")
            {
                await ProcessarPagamentoAsync(id, ct);
                return;
            }

            string? assinaturaId = tipo switch
            {
                "subscription_preapproval" or "preapproval" => id,
                // Cobranca do mes: so interessa para reler a assinatura (se o
                // cartao falhar e o Mercado Pago desistir, ela vira paused/cancelled).
                "subscription_authorized_payment" => await AssinaturaDaCobrancaAsync(id, ct),
                _ => null
            };
            if (assinaturaId == null) return;

            using var req = Requisicao(HttpMethod.Get, $"/preapproval/{Uri.EscapeDataString(assinaturaId)}");
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Lanca para o aviso voltar com erro e o Mercado Pago tentar de novo.
                throw new InvalidOperationException($"Mercado Pago: GET /preapproval/{assinaturaId} -> HTTP {(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            var raiz = doc.RootElement;
            var status = Texto(raiz, "status") ?? "";
            var referencia = Texto(raiz, "external_reference");
            decimal valor = 0;
            if (raiz.TryGetProperty("auto_recurring", out var rec) && rec.TryGetProperty("transaction_amount", out var v)
                && v.ValueKind == JsonValueKind.Number)
                valor = v.GetDecimal();

            if (!LerReferencia(referencia, out var tenantId, out var planoId))
            {
                _logger.LogInformation("Aviso do Mercado Pago ignorado: assinatura {Id} sem referencia do ConnectVeiculos", assinaturaId);
                return;
            }

            var tenant = await _tenants.GetByIdAsync(tenantId, ct);
            var plano = await _planos.GetByIdAsync(planoId, ct);
            if (tenant == null || plano == null)
            {
                _logger.LogWarning("Assinatura {Id}: loja {Tenant} ou plano {Plano} nao existe", assinaturaId, tenantId, planoId);
                return;
            }

            var decisao = Decidir(tenant, assinaturaId, status, valor, plano.PlaPreco);
            switch (decisao)
            {
                case Decisao.Ativar:
                    var anterior = tenant.TenAssinaturaStatus == StatusAssinatura.Ativa ? tenant.TenAssinaturaId : null;
                    await _tenants.AtualizarAsync(tenantId, t =>
                    {
                        t.RegistrarAssinatura(assinaturaId, StatusAssinatura.Ativa, planoId);
                        t.AlterarPlano(planoId);
                    }, ct);
                    _logger.LogInformation("Loja {Tenant} agora no plano {Plano} (assinatura {Id})", tenantId, plano.PlaNome, assinaturaId);

                    // Troca de plano: a assinatura antiga seguiria cobrando.
                    if (anterior != null && anterior != assinaturaId
                        && !await MudarStatusNoMercadoPagoAsync(anterior, StatusAssinatura.Cancelada, ct))
                        _logger.LogError("Assinatura antiga {Antiga} da loja {Tenant} NAO foi cancelada: cancele no painel do Mercado Pago", anterior, tenantId);
                    break;

                case Decisao.VoltarAoGratuito:
                    var free = await PlanoGratuitoAsync(ct);
                    await _tenants.AtualizarAsync(tenantId, t =>
                    {
                        t.RegistrarAssinatura(assinaturaId, status, t.TenAssinaturaPlaId);
                        if (free.HasValue) t.AlterarPlano(free.Value);
                    }, ct);
                    _logger.LogInformation("Assinatura {Id} da loja {Tenant} ficou {Status}: plano voltou ao gratuito", assinaturaId, tenantId, status);
                    break;

                case Decisao.ValorAbaixoDoPlano:
                    _logger.LogWarning("Assinatura {Id} da loja {Tenant}: valor {Valor} abaixo do plano {Plano} ({Preco}); plano NAO alterado",
                        assinaturaId, tenantId, valor, plano.PlaNome, plano.PlaPreco);
                    break;
            }
        }

        // Pagamento avulso: so os Pix do plano (referencia "cvpix:"). As cobrancas
        // mensais do cartao tambem chegam como "payment", mas sem essa referencia.
        private async Task ProcessarPagamentoAsync(string pagamentoId, CancellationToken ct)
        {
            using var req = Requisicao(HttpMethod.Get, $"/v1/payments/{Uri.EscapeDataString(pagamentoId)}");
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Mercado Pago: GET /v1/payments/{pagamentoId} -> HTTP {(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(body);
            var raiz = doc.RootElement;
            if (!LerReferenciaPix(Texto(raiz, "external_reference"), out var tenantId, out var planoId)) return;

            var status = Texto(raiz, "status");
            if (status != "approved")
            {
                _logger.LogInformation("Pix {Id} da loja {Tenant}: {Status}", pagamentoId, tenantId, status);
                return;
            }

            decimal valor = raiz.TryGetProperty("transaction_amount", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0;
            var plano = await _planos.GetByIdAsync(planoId, ct);
            if (plano == null || await _tenants.GetByIdAsync(tenantId, ct) == null) return;
            if (valor < plano.PlaPreco)
            {
                _logger.LogWarning("Pix {Id} da loja {Tenant}: valor {Valor} abaixo do plano {Plano}; plano NAO alterado", pagamentoId, tenantId, valor, plano.PlaNome);
                return;
            }

            var contado = false;
            var t = await _tenants.AtualizarAsync(tenantId, x => contado = x.RegistrarPagamentoPix(pagamentoId, planoId, DiasPorPix, DateTime.UtcNow), ct);
            if (contado)
                _logger.LogInformation("Pix {Id} aprovado: loja {Tenant} no plano {Plano} ate {Ate:u}", pagamentoId, tenantId, plano.PlaNome, t.TenPlanoPagoAte);
        }

        public static string ReferenciaPix(int tenantId, int planoId)
            => $"cvpix:{tenantId.ToString(CultureInfo.InvariantCulture)}:{planoId.ToString(CultureInfo.InvariantCulture)}";

        public static bool LerReferenciaPix(string? referencia, out int tenantId, out int planoId)
        {
            tenantId = planoId = 0;
            var partes = referencia?.Split(':');
            return partes is { Length: 3 } && partes[0] == "cvpix"
                && int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out tenantId)
                && int.TryParse(partes[2], NumberStyles.None, CultureInfo.InvariantCulture, out planoId);
        }

        public enum Decisao { Nada, Ativar, VoltarAoGratuito, ValorAbaixoDoPlano }

        /// <summary>
        /// O que fazer com a loja dado o estado da assinatura no Mercado Pago.
        /// Assinatura que nao e' a atual da loja (uma pendente antiga, ou a que
        /// foi substituida numa troca de plano) nao derruba o plano.
        /// </summary>
        public static Decisao Decidir(Tenant tenant, string assinaturaId, string status, decimal valor, decimal precoPlano)
        {
            var eAtual = tenant.TenAssinaturaId == assinaturaId;
            switch (status)
            {
                case StatusAssinatura.Ativa:
                    if (valor < precoPlano) return Decisao.ValorAbaixoDoPlano;
                    return eAtual && tenant.TenAssinaturaStatus == StatusAssinatura.Ativa ? Decisao.Nada : Decisao.Ativar;
                case StatusAssinatura.Pausada:
                case StatusAssinatura.Cancelada:
                    return eAtual && tenant.TenAssinaturaStatus == StatusAssinatura.Ativa ? Decisao.VoltarAoGratuito : Decisao.Nada;
                default:
                    return Decisao.Nada;
            }
        }

        /// <summary>
        /// Confere o cabecalho x-signature ("ts=...,v1=...") do aviso: HMAC-SHA256
        /// com a assinatura secreta sobre "id:{data.id};request-id:{x-request-id};ts:{ts};".
        /// </summary>
        public static bool AvisoAutentico(string? xSignature, string? xRequestId, string? dataId, string? segredo)
        {
            if (string.IsNullOrEmpty(segredo) || string.IsNullOrEmpty(xSignature) || string.IsNullOrEmpty(dataId))
                return false;

            string? ts = null, v1 = null;
            foreach (var parte in xSignature.Split(','))
            {
                var kv = parte.Split('=', 2);
                if (kv.Length != 2) continue;
                var chave = kv[0].Trim();
                if (chave == "ts") ts = kv[1].Trim();
                else if (chave == "v1") v1 = kv[1].Trim();
            }
            if (ts == null || v1 == null) return false;

            // O Mercado Pago assina o id em minusculas quando ele e' alfanumerico.
            var id = dataId.All(char.IsLetterOrDigit) ? dataId.ToLowerInvariant() : dataId;
            var manifesto = $"id:{id};";
            if (!string.IsNullOrEmpty(xRequestId)) manifesto += $"request-id:{xRequestId};";
            manifesto += $"ts:{ts};";

            var esperado = HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes(manifesto));
            byte[] recebido;
            try { recebido = Convert.FromHexString(v1); }
            catch (FormatException) { return false; }
            return CryptographicOperations.FixedTimeEquals(esperado, recebido);
        }

        public static string ReferenciaExterna(int tenantId, int planoId)
            => $"cv:{tenantId.ToString(CultureInfo.InvariantCulture)}:{planoId.ToString(CultureInfo.InvariantCulture)}";

        public static bool LerReferencia(string? referencia, out int tenantId, out int planoId)
        {
            tenantId = planoId = 0;
            var partes = referencia?.Split(':');
            return partes is { Length: 3 } && partes[0] == "cv"
                && int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out tenantId)
                && int.TryParse(partes[2], NumberStyles.None, CultureInfo.InvariantCulture, out planoId);
        }

        private async Task<string?> AssinaturaDaCobrancaAsync(string cobrancaId, CancellationToken ct)
        {
            using var req = Requisicao(HttpMethod.Get, $"/authorized_payments/{Uri.EscapeDataString(cobrancaId)}");
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Mercado Pago: GET /authorized_payments/{cobrancaId} -> HTTP {(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(body);
            var assinatura = Texto(doc.RootElement, "preapproval_id");
            _logger.LogInformation("Cobranca {Cobranca} da assinatura {Assinatura}: {Status}",
                cobrancaId, assinatura, Texto(doc.RootElement, "status"));
            return assinatura;
        }

        private async Task<bool> MudarStatusNoMercadoPagoAsync(string assinaturaId, string status, CancellationToken ct)
        {
            using var req = Requisicao(HttpMethod.Put, $"/preapproval/{Uri.EscapeDataString(assinaturaId)}");
            req.Content = JsonContent.Create(new { status });
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return true;
            _logger.LogWarning("Mercado Pago recusou mudar a assinatura {Id} para {Status}: HTTP {Http} {Body}",
                assinaturaId, status, (int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
            return false;
        }

        private async Task<int?> PlanoGratuitoAsync(CancellationToken ct)
            => (await _planos.ListarAtivosAsync(ct)).FirstOrDefault(p => p.PlaNome == Plano.NomeGratuito)?.PlaId;

        private HttpRequestMessage Requisicao(HttpMethod metodo, string caminho)
        {
            var req = new HttpRequestMessage(metodo, Api + caminho);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken.Trim());
            return req;
        }

        // O erro mais comum em teste: pagar com a mesma conta que recebe.
        private static string MensagemDoErro(string body)
        {
            var b = body.ToLowerInvariant();
            if (b.Contains("collector") && b.Contains("payer"))
                return "O e-mail do pagamento não pode ser o da conta que recebe. Use o e-mail da conta Mercado Pago de quem vai pagar.";
            if (b.Contains("payer_email"))
                return "O Mercado Pago não aceitou esse e-mail. Use o e-mail da sua conta Mercado Pago.";
            return "O Mercado Pago recusou criar a assinatura. Tente de novo em instantes.";
        }

        private static string? Texto(JsonElement e, string prop)
            => e.TryGetProperty(prop, out var v)
                ? v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString(),
                    JsonValueKind.Number => v.GetRawText(),
                    _ => null
                }
                : null;
    }
}
