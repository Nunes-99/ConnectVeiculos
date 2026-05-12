using ConnectVeiculos.Core.Entities.Leads;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeadsController : ControllerBase
    {
        private readonly ConnectVeiculosDbContext _context;

        public LeadsController(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        // POST publico - registrar lead
        [HttpPost]
        public async Task<IActionResult> Registrar([FromBody] RegistrarLeadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NomeCliente))
                return BadRequest("Nome do cliente e obrigatorio.");
            if (string.IsNullOrWhiteSpace(request.Telefone) && string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Informe ao menos um contato (telefone ou e-mail).");
            if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
                return BadRequest("E-mail invalido.");

            var lead = new Lead(0, request.VeiculoId, request.LojaId, request.NomeCliente,
                request.Telefone, request.Email, request.Origem, "NOVO", request.Observacao,
                request.Cpf, request.Renda, request.Entrada, request.Parcelas);

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            return Ok(new { id = lead.LeaId, mensagem = "Lead registrado com sucesso!" });
        }

        // GET - listar leads (autenticado)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Listar([FromQuery] int? lojaId = null, [FromQuery] string? status = null)
        {
            var query = _context.Leads.AsQueryable();
            if (lojaId.HasValue) query = query.Where(l => l.R_LojId == lojaId.Value);
            if (!string.IsNullOrEmpty(status)) query = query.Where(l => l.LeaStatus == status);
            var result = await query.OrderByDescending(l => l.LeaDtCriacao).ToListAsync();
            return Ok(result);
        }

        // PUT - atualizar status do lead
        [HttpPut("{id}/status")]
        [Authorize]
        public async Task<IActionResult> AtualizarStatus(int id, [FromBody] AtualizarLeadStatusRequest request)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();
            lead.AlterarStatus(request.Status);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET - relatorio de origens
        [HttpGet("relatorio/origens")]
        [Authorize]
        public async Task<IActionResult> RelatorioOrigens([FromQuery] int? lojaId = null)
        {
            var query = _context.Leads.AsQueryable();
            if (lojaId.HasValue) query = query.Where(l => l.R_LojId == lojaId.Value);

            var origens = await query
                .GroupBy(l => l.LeaOrigem)
                .Select(g => new { origem = g.Key, quantidade = g.Count() })
                .OrderByDescending(x => x.quantidade)
                .ToListAsync();

            return Ok(origens);
        }
    }

    public class RegistrarLeadRequest
    {
        public int? VeiculoId { get; set; }
        public int? LojaId { get; set; }
        public string NomeCliente { get; set; }
        public string Telefone { get; set; }
        public string Email { get; set; }
        public string Origem { get; set; }
        public string Observacao { get; set; }
        // Campos de financiamento
        public string Cpf { get; set; }
        public decimal? Renda { get; set; }
        public decimal? Entrada { get; set; }
        public int? Parcelas { get; set; }
    }

    public class AtualizarLeadStatusRequest
    {
        public string Status { get; set; }
    }
}
