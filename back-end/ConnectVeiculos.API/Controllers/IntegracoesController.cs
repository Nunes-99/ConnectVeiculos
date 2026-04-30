using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Services;
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
            [FromQuery] string? code,
            [FromQuery(Name = "error")] string? oauthError,
            [FromQuery(Name = "error_description")] string? oauthErrorDesc)
        {
            if (!string.IsNullOrEmpty(oauthError))
                return Content(BuildCallbackHtml(false, $"{oauthError}: {oauthErrorDesc}"), "text/html");

            if (string.IsNullOrEmpty(code))
                return Content(BuildCallbackHtml(false, "Codigo de autorizacao nao fornecido."), "text/html");

            try
            {
                await mlService.HandleCallbackAsync(code);
                return Content(BuildCallbackHtml(true, null), "text/html");
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

        [HttpPost("mercadolivre/notifications")]
        [AllowAnonymous]
        public IActionResult MercadoLivreNotifications(
            [FromServices] ILogger<IntegracoesController> logger,
            [FromBody] System.Text.Json.JsonElement payload)
        {
            // Webhook do ML: chega quando um anuncio muda de status, recebe pergunta etc.
            // Por enquanto so logamos. Processamento async pode ser plugado aqui (Hangfire/SignalR).
            logger.LogInformation("ML notification recebida: {Payload}", payload.ToString());
            return Ok();
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
                      : StatusCode(502, new { error = "Falha ao enviar mensagem (verifique configuracao e logs)." });
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
