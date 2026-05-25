using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IntegracoesController : ControllerBase
    {
        // ==========================================
        // MERCADO LIVRE
        // ==========================================

        [HttpGet("mercadolivre/auth-url")]
        public IActionResult GetMercadoLivreAuthUrl([FromServices] IMercadoLivreService mlService)
        {
            try
            {
                var url = mlService.GetAuthUrl();
                return Ok(new { url });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("mercadolivre/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> MercadoLivreCallback(
            [FromServices] IMercadoLivreService mlService,
             [FromServices] IOAuthStateProtector stateProtector,
             [FromServices] ITenantContext tenantContext,
            [FromQuery] string? code,
             [FromQuery] string? state,
            [FromQuery(Name = "error")] string? oauthError,
            [FromQuery(Name = "error_description")] string? oauthErrorDesc)
        {
            if (!string.IsNullOrEmpty(oauthError))
                return Content(BuildCallbackHtml(false, $"{oauthError}: {oauthErrorDesc}"), "text/html");

            if (string.IsNullOrEmpty(code))
                return Content(BuildCallbackHtml(false, "Código de autorização não fornecido."), "text/html");

             // ML so aceita 1 redirect_uri por app (cadastrado como dominio raiz
             // https://connectveiculos.dev.br/...), entao TODOS os callbacks chegam
             // resolvidos como tenant "default". O state cifrado carrega o slug
             // real — se for diferente, redireciona pro subdomain certo pra que o
             // TenantResolutionMiddleware aponte pro banco do tenant alvo.
             if (!string.IsNullOrEmpty(state))
             {
                 try
                 {
                     var payload = stateProtector.Decifrar(state);
                     var slugAtual = tenantContext.IsResolved ? tenantContext.TenantSlug : "default";
                     if (!string.IsNullOrEmpty(payload.TenantSlug)
                         && !string.Equals(payload.TenantSlug, slugAtual, StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(payload.TenantSlug, "default", StringComparison.OrdinalIgnoreCase))
                     {
                         // Monta URL no subdomain do tenant alvo preservando query string.
                         var host = Request.Host.Host;
                         // Se ja veio com subdomain (raro), troca-o; senao prefixa o slug.
                         var rootDomain = host.Contains('.')
                             ? string.Join('.', host.Split('.').AsEnumerable().Reverse().Take(3).Reverse())
                             : host;
                         var newUrl = $"{Request.Scheme}://{payload.TenantSlug}.{rootDomain}{Request.Path}{Request.QueryString}";
                         return Redirect(newUrl);
                     }
                 }
                 catch (OAuthStateException ex)
                 {
                     return Content(BuildCallbackHtml(false, ex.Message), "text/html");
                 }
             }

            try
            {
                 await mlService.HandleCallbackAsync(code, state);
                return Content(BuildCallbackHtml(true, null), "text/html");
            }
             catch (OAuthStateException ex)
             {
                 // State CSRF invalido — pode ser link velho (state expirado), callback
                 // chegando em outro tenant ou ataque. Resposta amigavel sem stack trace.
                 return Content(BuildCallbackHtml(false, ex.Message), "text/html");
             }
            catch (Exception ex)
            {
                return Content(BuildCallbackHtml(false, ex.Message), "text/html");
            }
        }

        private static string BuildCallbackHtml(bool sucesso, string? mensagemErro)
        {
            var titulo = sucesso ? "Mercado Livre conectado!" : "Falha na conexao";
            var cor = sucesso ? "#16a34a" : "#dc2626";
            var corpo = sucesso
                ? "<p>A integracao foi configurada com sucesso. Voce pode fechar esta janela.</p>"
                : $"<p>Nao foi possivel concluir a conexao.</p><pre style='background:#f3f4f6;padding:8px;border-radius:6px;overflow:auto;font-size:12px'>{System.Net.WebUtility.HtmlEncode(mensagemErro ?? "")}</pre>";
            return $@"<!doctype html><html lang='pt-br'><head><meta charset='utf-8'><title>{titulo}</title>
<style>body{{font-family:system-ui,sans-serif;max-width:520px;margin:80px auto;padding:24px;text-align:center}}
h1{{color:{cor};margin-bottom:16px}} button{{padding:8px 20px;border:0;background:#1a237e;color:#fff;border-radius:6px;cursor:pointer;font-size:14px}}</style></head>
<body><h1>{titulo}</h1>{corpo}<button onclick='window.close()'>Fechar</button>
<script>setTimeout(function(){{ try{{window.opener&&window.opener.postMessage({{ml:'{(sucesso ? "ok" : "fail")}'}},window.location.origin);}}catch(e){{}} if({sucesso.ToString().ToLower()}) setTimeout(function(){{window.close();}},1500); }},100);</script></body></html>";
        }

        [HttpGet("mercadolivre/status")]
        public async Task<IActionResult> GetMercadoLivreStatus([FromServices] IMercadoLivreService mlService)
        {
            var conectado = await mlService.IsConnectedAsync();
            return Ok(new { conectado });
        }

        [HttpGet("mercadolivre/info")]
        public async Task<IActionResult> GetMercadoLivreInfo([FromServices] IMercadoLivreService mlService)
        {
            var info = await mlService.GetContaInfoAsync();
            if (info == null) return Ok(new { conectado = false });
            return Ok(new { conectado = true, info });
        }

        [HttpPost("mercadolivre/desconectar")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> DesconectarMercadoLivre([FromServices] IMercadoLivreService mlService)
        {
            await mlService.DesconectarAsync();
            return Ok(new { mensagem = "Mercado Livre desconectado." });
        }

        [HttpPost("mercadolivre/publicar/{veiculoId}")]
        public async Task<IActionResult> PublicarMercadoLivre(
            [FromServices] IMercadoLivreService mlService,
            [FromServices] IVeiculoPublicacaoRepository pubRepo,
            int veiculoId)
        {
            // Verificar se ja existe publicacao ativa
            var existente = await pubRepo.GetAtivaByVeiculoEPlataformaAsync(veiculoId, "MercadoLivre");
            if (existente != null)
                return BadRequest("Veiculo ja possui anuncio ativo no Mercado Livre.");

            var (externoId, url) = await mlService.PublicarVeiculoAsync(veiculoId);

            var publicacao = new VeiculoPublicacao(veiculoId, "MercadoLivre", externoId, url);
            await pubRepo.CreateAsync(publicacao);

            return Ok(new { externoId, url, mensagem = "Anuncio publicado com sucesso!" });
        }

        [HttpPost("mercadolivre/sincronizar-disponiveis")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> SincronizarDisponiveisMercadoLivre(
            [FromServices] IMercadoLivreService mlService,
            [FromServices] IVeiculoRepository veiculoRepo,
            [FromServices] IVeiculoPublicacaoRepository pubRepo,
            [FromServices] ILogger<IntegracoesController> logger)
        {
            if (!await mlService.IsConnectedAsync())
                return BadRequest(new { error = "Mercado Livre nao esta conectado." });

            var todos = await veiculoRepo.GetAllAsync();
            var disponiveis = todos.Where(v => v.VeiSts == "D").ToList();

            int novosPublicados = 0;
            int jaPublicados = 0;
            var falhas = new List<object>();

            foreach (var veiculo in disponiveis)
            {
                try
                {
                    var existente = await pubRepo.GetAtivaByVeiculoEPlataformaAsync(veiculo.VeiId, "MercadoLivre");
                    if (existente != null) { jaPublicados++; continue; }

                    var (externoId, url) = await mlService.PublicarVeiculoAsync(veiculo.VeiId);
                    await pubRepo.CreateAsync(new VeiculoPublicacao(veiculo.VeiId, "MercadoLivre", externoId, url));
                    novosPublicados++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Erro ao publicar veiculo {Id} no ML em sincronizacao em massa", veiculo.VeiId);
                    falhas.Add(new
                    {
                        veiculoId = veiculo.VeiId,
                        descricao = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno} ({veiculo.VeiPlaca})",
                        erro = ex.Message
                    });
                }
            }

            return Ok(new
            {
                totalDisponiveis = disponiveis.Count,
                novosPublicados,
                jaPublicados,
                falhas
            });
        }

        [HttpPost("mercadolivre/notifications")]
        [AllowAnonymous]
        public async Task<IActionResult> MercadoLivreNotifications(
            [FromServices] IMercadoLivreService mlService,
            [FromServices] ILogger<IntegracoesController> logger,
            [FromBody] System.Text.Json.JsonElement payload)
        {
            // Webhook do ML envia POST com body { topic, resource, user_id, ... }.
            // Sempre respondemos 200 rapido (ML retenta se nao recebe 2xx em 22s),
            // entao tratamos excecoes localmente e logamos.
            logger.LogInformation("ML notification recebida: {Payload}", payload.ToString());
            try
            {
                var topic = payload.TryGetProperty("topic", out var t) ? t.GetString() : null;
                var resource = payload.TryGetProperty("resource", out var r) ? r.GetString() : null;
                await mlService.ProcessarNotificacaoAsync(topic ?? "", resource ?? "");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro processando webhook ML — respondendo 200 mesmo assim pra ML nao retentar.");
            }
            return Ok();
        }

         [HttpGet("mercadolivre/logs")]
         [Authorize(Roles = "Administrador,Gerente")]
         public async Task<IActionResult> GetMercadoLivreLogs(
             [FromServices] Core.Interfaces.Database.Repositories.Integracoes.IIntegracaoLogRepository logRepo,
             [FromQuery] int limit = 100)
         {
             var logs = await logRepo.GetUltimosAsync(limit);
             return Ok(logs);
         }

        [HttpDelete("mercadolivre/remover/{veiculoId}")]
        public async Task<IActionResult> RemoverMercadoLivre(
            [FromServices] IMercadoLivreService mlService,
            [FromServices] IVeiculoPublicacaoRepository pubRepo,
            int veiculoId)
        {
            var publicacao = await pubRepo.GetAtivaByVeiculoEPlataformaAsync(veiculoId, "MercadoLivre");
            if (publicacao == null)
                return NotFound("Nenhum anuncio ativo encontrado no Mercado Livre.");

            await mlService.RemoverAnuncioAsync(publicacao.PubExternoId);
            publicacao.Remover();
            await pubRepo.UpdateAsync(publicacao);

            return Ok(new { mensagem = "Anuncio removido com sucesso!" });
        }

        // ==========================================
        // WHATSAPP BUSINESS API
        // ==========================================

        [HttpGet("whatsapp/status")]
        public async Task<IActionResult> WhatsAppStatus([FromServices] IWhatsAppService whatsApp)
        {
            return Ok(new { configurado = await whatsApp.IsConfiguredAsync() });
        }

        [HttpGet("whatsapp/config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> GetWhatsAppConfig([FromServices] IWhatsAppService whatsApp)
        {
            var info = await whatsApp.GetConfigAsync();
            return Ok(info);
        }

        [HttpPost("whatsapp/config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> SaveWhatsAppConfig(
            [FromServices] IWhatsAppService whatsApp,
            [FromBody] SalvarWhatsAppConfigRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.PhoneId) || string.IsNullOrWhiteSpace(request.VerifyToken))
                return BadRequest(new { error = "AccessToken, PhoneId e VerifyToken sao obrigatorios." });

            await whatsApp.SalvarConfigAsync(request.AccessToken, request.PhoneId, request.VerifyToken);
            return Ok(new { mensagem = "Configuracao salva." });
        }

        [HttpPost("whatsapp/desconectar")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> DesconectarWhatsApp([FromServices] IWhatsAppService whatsApp)
        {
            await whatsApp.DesconectarAsync();
            return Ok(new { mensagem = "WhatsApp desconectado." });
        }

        // Webhook verificacao (Meta envia hub.challenge na primeira validacao)
        [HttpGet("whatsapp/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> WhatsAppVerify(
            [FromServices] IWhatsAppService whatsApp,
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            var verifyToken = await whatsApp.GetVerifyTokenAsync() ?? "connectveiculos-verify";
            if (mode == "subscribe" && token == verifyToken)
                return Content(challenge ?? "", "text/plain");
            return Forbid();
        }

        // Webhook recebimento de mensagens / eventos
        [HttpPost("whatsapp/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> WhatsAppReceive(
            [FromServices] ILogger<IntegracoesController> logger,
            [FromServices] ConnectVeiculos.Infrastructure.Database.EntityFramework.ConnectVeiculosDbContext db,
            [FromServices] ConnectVeiculos.Core.Interfaces.Services.INotificacaoService notificacao,
            [FromBody] System.Text.Json.JsonElement payload)
        {
            logger.LogInformation("WhatsApp webhook recebido");

            try
            {
                // Estrutura do payload Meta:
                // { "entry":[{"changes":[{"value":{"messages":[{"from","text":{"body"}}],"contacts":[{"profile":{"name"}}]}}]}] }
                if (!payload.TryGetProperty("entry", out var entry) || entry.GetArrayLength() == 0) return Ok();

                foreach (var ent in entry.EnumerateArray())
                {
                    if (!ent.TryGetProperty("changes", out var changes)) continue;
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value)) continue;
                        if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != System.Text.Json.JsonValueKind.Array) continue;

                        // Mapear contatos por wa_id
                        var nomes = new Dictionary<string, string>();
                        if (value.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var c in contacts.EnumerateArray())
                            {
                                var waId = c.TryGetProperty("wa_id", out var w) ? w.GetString() : null;
                                var nome = c.TryGetProperty("profile", out var p) && p.TryGetProperty("name", out var n) ? n.GetString() : null;
                                if (!string.IsNullOrEmpty(waId) && !string.IsNullOrEmpty(nome)) nomes[waId] = nome;
                            }
                        }

                        foreach (var msg in messages.EnumerateArray())
                        {
                            var from = msg.TryGetProperty("from", out var f) ? f.GetString() ?? "" : "";
                            if (string.IsNullOrEmpty(from)) continue;

                            var body = "";
                            if (msg.TryGetProperty("text", out var text) && text.TryGetProperty("body", out var t))
                                body = t.GetString() ?? "";

                            var nomeCliente = nomes.TryGetValue(from, out var n2) ? n2 : from;
                            var telefoneFmt = "+" + new string(from.Where(char.IsDigit).ToArray());

                            // Evitar duplicar lead aberto pra mesmo telefone (ultimas 24h)
                            var limite = DateTime.Now.AddHours(-24);
                            var ja = await db.Leads.AsNoTracking()
                                .Where(l => l.LeaTelefone == telefoneFmt && l.LeaOrigem == "WHATSAPP" && l.LeaDtCriacao >= limite)
                                .FirstOrDefaultAsync();
                            if (ja != null)
                            {
                                logger.LogInformation("Lead WhatsApp ja existe ({Tel}), ignorando duplicata", telefoneFmt);
                                continue;
                            }

                            var lead = new ConnectVeiculos.Core.Entities.Leads.Lead(
                                0, null, null, nomeCliente, telefoneFmt, "",
                                "WHATSAPP", "NOVO",
                                string.IsNullOrEmpty(body) ? null : "[WhatsApp] " + body);
                            db.Leads.Add(lead);
                            await db.SaveChangesAsync();

                            logger.LogInformation("Lead WhatsApp criado #{Id} de {Tel}", lead.LeaId, telefoneFmt);

                            try
                            {
                                await notificacao.EnviarParaTodosAsync("LEAD_WHATSAPP", new
                                {
                                    leadId = lead.LeaId,
                                    nome = nomeCliente,
                                    telefone = telefoneFmt,
                                    mensagem = body
                                });
                            }
                            catch (Exception ex) { logger.LogWarning(ex, "Falha SignalR LEAD_WHATSAPP"); }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro processando webhook WhatsApp");
            }

            // Sempre 200 — Meta retentaria se receber !=200
            return Ok();
        }

        [HttpPost("whatsapp/enviar")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> WhatsAppEnviar(
            [FromServices] IWhatsAppService whatsApp,
            [FromBody] EnviarWhatsAppRequest request)
        {
            if (string.IsNullOrEmpty(request.Telefone) || string.IsNullOrEmpty(request.Mensagem))
                return BadRequest("Telefone e mensagem sao obrigatorios.");

            var ok = await whatsApp.EnviarMensagemAsync(request.Telefone, request.Mensagem);
            return ok ? Ok(new { mensagem = "Mensagem enviada." })
                      : StatusCode(502, new { error = "Falha ao enviar mensagem (verifique configuração e logs)." });
        }

        // ==========================================
        // E-MAIL / SMTP
        // ==========================================

        [HttpGet("smtp/config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> GetSmtpConfig([FromServices] IEmailService email)
        {
            var info = await email.GetConfigAsync();
            return Ok(info);
        }

        [HttpPost("smtp/config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> SaveSmtpConfig(
            [FromServices] IEmailService email,
            [FromBody] SalvarSmtpConfigRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SmtpServer) || string.IsNullOrWhiteSpace(request.SenderEmail) || string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { error = "SmtpServer, SenderEmail e Username sao obrigatorios." });

            await email.SalvarConfigAsync(new EmailConfigInput
            {
                SmtpServer = request.SmtpServer,
                SmtpPort = request.SmtpPort > 0 ? request.SmtpPort : 587,
                Username = request.Username,
                Password = request.Password ?? "",
                SenderEmail = request.SenderEmail,
                SenderName = request.SenderName ?? "ConnectVeiculos",
                EnableSsl = request.EnableSsl
            });
            return Ok(new { mensagem = "Configuracao SMTP salva." });
        }

        [HttpPost("smtp/desconectar")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> DesconectarSmtp([FromServices] IEmailService email)
        {
            await email.DesconectarAsync();
            return Ok(new { mensagem = "SMTP desconectado." });
        }

        [HttpPost("smtp/test")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> TestarSmtp(
            [FromServices] IEmailService email,
            [FromBody] TestarSmtpRequest request)
        {
            var result = await email.TestarEnvioAsync(request.Destinatario);
            return result.Sucesso ? Ok(result) : StatusCode(502, result);
        }

        // ==========================================
        // FACEBOOK CATALOG
        // ==========================================

        [HttpGet("facebook/status")]
        public async Task<IActionResult> FacebookStatus([FromServices] IFacebookCatalogService fb)
        {
            return Ok(new { configurado = await fb.IsConfiguredAsync() });
        }

        [HttpGet("facebook/config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> GetFacebookConfig([FromServices] IFacebookCatalogService fb)
        {
            return Ok(await fb.GetConfigAsync());
        }

        [HttpPost("facebook/config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> SaveFacebookConfig(
            [FromServices] IFacebookCatalogService fb,
            [FromBody] FacebookConfigInput request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.CatalogId))
                return BadRequest(new { error = "AccessToken e CatalogId sao obrigatorios." });

            await fb.SalvarConfigAsync(request);
            return Ok(new { mensagem = "Configuracao do Facebook Catalog salva." });
        }

        [HttpPost("facebook/desconectar")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> DesconectarFacebook([FromServices] IFacebookCatalogService fb)
        {
            await fb.DesconectarAsync();
            return Ok(new { mensagem = "Facebook Catalog desconectado." });
        }

        [HttpPost("facebook/test")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> TestarFacebook([FromServices] IFacebookCatalogService fb)
        {
            var result = await fb.TestarAsync();
            return result.Sucesso ? Ok(result) : StatusCode(502, result);
        }

        [HttpGet("facebook/verification-code")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> GetFacebookVerificationCode(
            [FromServices] ITenantContext tenantContext,
            [FromServices] ITenantStore store)
        {
            if (!tenantContext.IsResolved) return BadRequest(new { error = "Tenant nao resolvido." });
            var tenant = await store.GetByIdAsync(tenantContext.TenantId);
            return Ok(new { code = tenant?.TenFacebookVerifCode });
        }

        [HttpPut("facebook/verification-code")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> SetFacebookVerificationCode(
            [FromServices] ITenantContext tenantContext,
            [FromServices] ITenantStore store,
            [FromBody] VerificationCodeRequest request)
        {
            if (!tenantContext.IsResolved) return BadRequest(new { error = "Tenant nao resolvido." });
            // Sanitizacao basica: aceita apenas o "content" da meta tag.
            // Strings vazias limpam o codigo. Tudo que nao bate com o padrao esperado eh rejeitado.
            var code = (request.Code ?? string.Empty).Trim();
            if (code.Length > 128) return BadRequest(new { error = "Codigo muito longo." });
            if (!string.IsNullOrEmpty(code) && !System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Za-z0-9_\-]+$"))
                return BadRequest(new { error = "Codigo invalido. Cole apenas o valor do atributo content da meta tag." });

            await store.UpdateVerificationCodesAsync(tenantContext.TenantId, googleCode: null, facebookCode: code);
            return Ok(new { mensagem = string.IsNullOrEmpty(code) ? "Codigo Facebook removido." : "Codigo Facebook salvo." });
        }

        // ==========================================
        // GOOGLE MERCHANT CENTER
        // ==========================================

        [HttpGet("google/status")]
        public async Task<IActionResult> GoogleStatus([FromServices] IGoogleMerchantService gm)
        {
            return Ok(new { configurado = await gm.IsConfiguredAsync() });
        }

        [HttpGet("google/config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> GetGoogleConfig([FromServices] IGoogleMerchantService gm)
        {
            return Ok(await gm.GetConfigAsync());
        }

        [HttpPost("google/config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> SaveGoogleConfig(
            [FromServices] IGoogleMerchantService gm,
            [FromBody] GoogleMerchantConfigInput request)
        {
            if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.MerchantId))
                return BadRequest(new { error = "ClientId e MerchantId sao obrigatorios." });

            // ClientSecret e RefreshToken sao obrigatorios apenas se ainda nao foram salvos;
            // se ja existem no banco, omitir mantem o valor atual (suporte em SalvarConfigAsync).
            var existente = await gm.GetConfigAsync();
            if (string.IsNullOrWhiteSpace(request.ClientSecret) && !existente.ClientSecretDefinido)
                return BadRequest(new { error = "ClientSecret e obrigatorio." });
            if (string.IsNullOrWhiteSpace(request.RefreshToken) && !existente.RefreshTokenDefinido)
                return BadRequest(new { error = "RefreshToken e obrigatorio." });

            await gm.SalvarConfigAsync(request);
            return Ok(new { mensagem = "Configuracao do Google Merchant salva." });
        }

        [HttpPost("google/desconectar")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> DesconectarGoogle([FromServices] IGoogleMerchantService gm)
        {
            await gm.DesconectarAsync();
            return Ok(new { mensagem = "Google Merchant desconectado." });
        }

        [HttpPost("google/test")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> TestarGoogle([FromServices] IGoogleMerchantService gm)
        {
            var result = await gm.TestarAsync();
            return result.Sucesso ? Ok(result) : StatusCode(502, result);
        }

        [HttpGet("google/verification-code")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> GetGoogleVerificationCode(
            [FromServices] ITenantContext tenantContext,
            [FromServices] ITenantStore store)
        {
            if (!tenantContext.IsResolved) return BadRequest(new { error = "Tenant nao resolvido." });
            var tenant = await store.GetByIdAsync(tenantContext.TenantId);
            return Ok(new { code = tenant?.TenGoogleVerifCode });
        }

        [HttpPut("google/verification-code")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> SetGoogleVerificationCode(
            [FromServices] ITenantContext tenantContext,
            [FromServices] ITenantStore store,
            [FromBody] VerificationCodeRequest request)
        {
            if (!tenantContext.IsResolved) return BadRequest(new { error = "Tenant nao resolvido." });
            var code = (request.Code ?? string.Empty).Trim();
            if (code.Length > 128) return BadRequest(new { error = "Codigo muito longo." });
            if (!string.IsNullOrEmpty(code) && !System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Za-z0-9_\-]+$"))
                return BadRequest(new { error = "Codigo invalido. Cole apenas o valor do atributo content da meta tag." });

            await store.UpdateVerificationCodesAsync(tenantContext.TenantId, googleCode: code, facebookCode: null);
            return Ok(new { mensagem = string.IsNullOrEmpty(code) ? "Codigo Google removido." : "Codigo Google salvo." });
        }

        // ==========================================
        // VERIFICATION CODES PUBLIC (consumido pelo SSR)
        // ==========================================

        /// <summary>
        /// Lista agregada de codigos de verificacao de TODOS os tenants ativos.
        /// Usado pelo Angular SSR para injetar meta tags no &lt;head&gt; — cada
        /// tenant que tiver codigo cadastrado contribui com uma meta tag, permitindo
        /// que multiplas contas Google/Meta verifiquem o mesmo dominio raiz.
        /// </summary>
        [HttpGet("verification-codes")]
        [AllowAnonymous]
        [ResponseCache(Duration = 300)]
        public async Task<IActionResult> ListVerificationCodes([FromServices] ITenantStore store)
        {
            var tenants = await store.ListActiveAsync(HttpContext.RequestAborted);
            var google = tenants.Where(t => !string.IsNullOrWhiteSpace(t.TenGoogleVerifCode))
                                .Select(t => t.TenGoogleVerifCode!).ToList();
            var facebook = tenants.Where(t => !string.IsNullOrWhiteSpace(t.TenFacebookVerifCode))
                                  .Select(t => t.TenFacebookVerifCode!).ToList();
            return Ok(new { google, facebook });
        }

        public class VerificationCodeRequest
        {
            public string? Code { get; set; }
        }

        // ==========================================
        // PUBLICACOES
        // ==========================================

        [HttpGet("publicacoes/{veiculoId}")]
        public async Task<IActionResult> GetPublicacoes(
            [FromServices] IVeiculoPublicacaoRepository pubRepo,
            int veiculoId)
        {
            var publicacoes = await pubRepo.GetByVeiculoIdAsync(veiculoId);
            return Ok(publicacoes.Select(p => new
            {
                p.PubId,
                p.PubPlataforma,
                p.PubExternoId,
                p.PubStatus,
                p.PubUrl,
                p.PubDtPublicacao,
                p.PubDtRemocao
            }));
        }
    }

    public class EnviarWhatsAppRequest
    {
        public string Telefone { get; set; } = "";
        public string Mensagem { get; set; } = "";
    }

    public class SalvarWhatsAppConfigRequest
    {
        public string AccessToken { get; set; } = "";
        public string PhoneId { get; set; } = "";
        public string VerifyToken { get; set; } = "";
    }

    public class SalvarSmtpConfigRequest
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string SenderEmail { get; set; } = "";
        public string SenderName { get; set; } = "ConnectVeiculos";
        public bool EnableSsl { get; set; } = true;
    }

    public class TestarSmtpRequest
    {
        public string Destinatario { get; set; } = "";
    }
}
