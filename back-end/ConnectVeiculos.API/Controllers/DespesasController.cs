using ConnectVeiculos.Core.Entities.Despesas;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DespesasController : ControllerBase
    {
        private readonly ConnectVeiculosDbContext _context;

        public DespesasController(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        // GET - listar despesas por veiculo
        [HttpGet("veiculo/{veiculoId}")]
        public async Task<IActionResult> ListarPorVeiculo(int veiculoId)
        {
            var despesas = await _context.VeiculosDespesas
                .Where(d => d.R_VeiId == veiculoId)
                .OrderByDescending(d => d.DesDtDespesa)
                .ToListAsync();
            return Ok(despesas);
        }

        // GET - total de despesas por veiculo
        [HttpGet("veiculo/{veiculoId}/total")]
        public async Task<IActionResult> TotalPorVeiculo(int veiculoId)
        {
            var total = await _context.VeiculosDespesas
                .Where(d => d.R_VeiId == veiculoId)
                .SumAsync(d => d.DesValor);
            return Ok(new { veiculoId, total });
        }

        // POST - criar despesa
        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] CriarDespesaRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Tipo))
                return BadRequest("Tipo da despesa e obrigatorio.");
            if (request.Valor <= 0)
                return BadRequest("Valor da despesa deve ser maior que zero.");
            if (request.VeiculoId <= 0)
                return BadRequest("Veiculo invalido.");

            var despesa = new VeiculoDespesa(0, request.VeiculoId, request.Tipo, request.Descricao, request.Valor, request.DataDespesa);
            _context.VeiculosDespesas.Add(despesa);
            await _context.SaveChangesAsync();

            return Ok(new { id = despesa.DesId, mensagem = "Despesa registrada com sucesso!" });
        }

        // DELETE - remover despesa
        [HttpDelete("{id}")]
        public async Task<IActionResult> Remover(int id)
        {
            var despesa = await _context.VeiculosDespesas.FindAsync(id);
            if (despesa == null) return NotFound();
            _context.VeiculosDespesas.Remove(despesa);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CriarDespesaRequest
    {
        public int VeiculoId { get; set; }
        public string Tipo { get; set; }
        public string Descricao { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataDespesa { get; set; }
    }
}
