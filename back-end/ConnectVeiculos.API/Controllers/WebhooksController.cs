using ConnectVeiculos.Core.Entities.Webhooks;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")]
    public class WebhooksController : ControllerBase
    {
        private readonly IWebhookRepository _webhookRepository;

        public WebhooksController(IWebhookRepository webhookRepository)
        {
            _webhookRepository = webhookRepository;
        }

        /// <summary>
        /// Lista todos os webhooks cadastrados
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var webhooks = await _webhookRepository.GetAllAsync();
            return Ok(webhooks.Select(w => new
            {
                w.WebId,
                w.WebUrl,
                Eventos = JsonSerializer.Deserialize<string[]>(w.WebEventos),
                w.WebAtivo,
                w.WebCriadoEm,
                w.WebUltimaExecucao,
                w.WebFalhasConsecutivas
            }));
        }

        /// <summary>
        /// Cadastra um novo webhook
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WebhookInputModel input)
        {
            var secret = Guid.NewGuid().ToString("N");
            var webhook = new Webhook(
                input.Url,
                JsonSerializer.Serialize(input.Eventos),
                secret);

            await _webhookRepository.AddAsync(webhook);

            return CreatedAtAction(nameof(GetById), new { id = webhook.WebId }, new
            {
                webhook.WebId,
                webhook.WebUrl,
                input.Eventos,
                Secret = secret,
                Mensagem = "Guarde o secret, ele nao sera exibido novamente."
            });
        }

        /// <summary>
        /// Obtem um webhook por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var webhook = await _webhookRepository.GetByIdAsync(id);
            if (webhook == null)
                return NotFound();

            return Ok(new
            {
                webhook.WebId,
                webhook.WebUrl,
                Eventos = JsonSerializer.Deserialize<string[]>(webhook.WebEventos),
                webhook.WebAtivo,
                webhook.WebCriadoEm,
                webhook.WebUltimaExecucao,
                webhook.WebFalhasConsecutivas
            });
        }

        /// <summary>
        /// Ativa ou desativa um webhook
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ToggleStatus(int id, [FromBody] bool ativo)
        {
            var webhook = await _webhookRepository.GetByIdAsync(id);
            if (webhook == null)
                return NotFound();

            webhook.WebAtivo = ativo;
            webhook.WebFalhasConsecutivas = 0;
            await _webhookRepository.UpdateAsync(webhook);

            return NoContent();
        }

        /// <summary>
        /// Remove um webhook
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _webhookRepository.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Lista eventos disponiveis para webhook
        /// </summary>
        [HttpGet("eventos")]
        [AllowAnonymous]
        public IActionResult GetEventosDisponiveis()
        {
            return Ok(new[]
            {
                new { Evento = WebhookEventos.VendaCriada, Descricao = "Disparado quando uma nova venda e registrada" },
                new { Evento = WebhookEventos.VendaEstornada, Descricao = "Disparado quando uma venda e estornada" },
                new { Evento = WebhookEventos.VeiculoCadastrado, Descricao = "Disparado quando um novo veiculo e cadastrado" },
                new { Evento = WebhookEventos.VeiculoVendido, Descricao = "Disparado quando um veiculo e marcado como vendido" },
                new { Evento = WebhookEventos.VeiculoPrecoAlterado, Descricao = "Disparado quando o preco de um veiculo e alterado" },
                new { Evento = WebhookEventos.UsuarioCadastrado, Descricao = "Disparado quando um novo usuario e cadastrado" }
            });
        }
    }

    public class WebhookInputModel
    {
        public string Url { get; set; }
        public string[] Eventos { get; set; }
    }
}
