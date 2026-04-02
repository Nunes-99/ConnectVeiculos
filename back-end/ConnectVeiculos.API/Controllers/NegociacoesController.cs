using ConnectVeiculos.Core.Entities.Negociacoes;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NegociacoesController : ControllerBase
    {
        private readonly ConnectVeiculosDbContext _context;

        public NegociacoesController(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] int? veiculoId = null, [FromQuery] int? lojaId = null, [FromQuery] string? status = null)
        {
            var query = _context.Negociacoes.AsQueryable();
            if (veiculoId.HasValue) query = query.Where(n => n.R_VeiId == veiculoId.Value);
            if (lojaId.HasValue) query = query.Where(n => n.R_LojId == lojaId.Value);
            if (!string.IsNullOrEmpty(status)) query = query.Where(n => n.NegStatus == status);
            var result = await query.OrderByDescending(n => n.NegDtCriacao).ToListAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObterPorId(int id)
        {
            var negociacao = await _context.Negociacoes.FindAsync(id);
            if (negociacao == null) return NotFound();
            return Ok(negociacao);
        }

        [HttpGet("veiculo/{veiculoId}")]
        public async Task<IActionResult> ListarPorVeiculo(int veiculoId)
        {
            var result = await _context.Negociacoes
                .Where(n => n.R_VeiId == veiculoId)
                .OrderByDescending(n => n.NegDtCriacao)
                .ToListAsync();
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Registrar([FromBody] RegistrarNegociacaoRequest request)
        {
            var negociacao = new Negociacao(0, request.VeiculoId, request.LojaId, request.NomeCliente,
                request.Telefone, request.Email, request.ValorProposta, request.Status, request.Observacao);

            _context.Negociacoes.Add(negociacao);
            await _context.SaveChangesAsync();

            return Ok(new { id = negociacao.NegId, mensagem = "Negociacao registrada com sucesso!" });
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> AtualizarStatus(int id, [FromBody] AtualizarNegociacaoStatusRequest request)
        {
            var negociacao = await _context.Negociacoes.FindAsync(id);
            if (negociacao == null) return NotFound();
            negociacao.AlterarStatus(request.Status);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Atualizar(int id, [FromBody] RegistrarNegociacaoRequest request)
        {
            var negociacao = await _context.Negociacoes.FindAsync(id);
            if (negociacao == null) return NotFound();
            negociacao.AtualizarProposta(request.ValorProposta, request.Observacao);
            negociacao.AlterarStatus(request.Status ?? negociacao.NegStatus);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Excluir(int id)
        {
            var negociacao = await _context.Negociacoes.FindAsync(id);
            if (negociacao == null) return NotFound();
            _context.Negociacoes.Remove(negociacao);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class RegistrarNegociacaoRequest
    {
        public int VeiculoId { get; set; }
        public int? LojaId { get; set; }
        public string NomeCliente { get; set; }
        public string Telefone { get; set; }
        public string Email { get; set; }
        public decimal ValorProposta { get; set; }
        public string Status { get; set; }
        public string Observacao { get; set; }
    }

    public class AtualizarNegociacaoStatusRequest
    {
        public string Status { get; set; }
    }
}
