using ConnectVeiculos.Core.Entities.Documentos;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/veiculos-documentos")]
    [Authorize]
    public class VeiculoDocumentosController : ControllerBase
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoDocumentosController(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        [HttpGet("veiculo/{veiculoId}")]
        public async Task<IActionResult> ListarPorVeiculo(int veiculoId)
        {
            var documentos = await _context.VeiculosDocumentos
                .Where(d => d.R_VeiId == veiculoId)
                .OrderByDescending(d => d.DocDtCriacao)
                .ToListAsync();
            return Ok(documentos);
        }

        [HttpGet("vencendo")]
        public async Task<IActionResult> ListarVencendo([FromQuery] int diasAFrente = 30)
        {
            var limite = DateTime.Today.AddDays(diasAFrente);
            var documentos = await _context.VeiculosDocumentos
                .Where(d => d.DocDtVencimento != null && d.DocDtVencimento <= limite && d.DocStatus != "CONCLUIDO")
                .OrderBy(d => d.DocDtVencimento)
                .ToListAsync();
            return Ok(documentos);
        }

        // Os da tela (Laudo Cautelar, Vistoria, Licenciamento, Em andamento) e os
        // antigos da API. A tela mandava tipos que a API recusava.
        private static readonly string[] TiposValidos =
            { "CRLV", "LAUDO_CAUTELAR", "LAUDO", "VISTORIA", "TRANSFERENCIA", "IPVA", "LICENCIAMENTO", "SEGURO", "MULTA", "FINANCIAMENTO", "OUTROS" };
        private static readonly string[] StatusValidos = { "PENDENTE", "EM_ANDAMENTO", "EM_DIA", "VENCIDO", "CONCLUIDO" };

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] CriarDocumentoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Tipo))
                return BadRequest(new { error = "Tipo do documento é obrigatório." });
            if (request.VeiculoId <= 0)
                return BadRequest(new { error = "Veículo inválido." });
            if (!TiposValidos.Contains(request.Tipo.ToUpperInvariant()))
                return BadRequest(new { error = $"Tipo inválido. Use um destes: {string.Join(", ", TiposValidos)}." });
            var statusFinal = string.IsNullOrEmpty(request.Status) ? "PENDENTE" : request.Status.ToUpperInvariant();
            if (!StatusValidos.Contains(statusFinal))
                return BadRequest(new { error = $"Status inválido. Use um destes: {string.Join(", ", StatusValidos)}." });

            var doc = new VeiculoDocumento(0, request.VeiculoId, request.Tipo.ToUpperInvariant(),
                statusFinal, request.Arquivo, request.Observacao, request.DataVencimento);

            _context.VeiculosDocumentos.Add(doc);
            await _context.SaveChangesAsync();
            return Ok(new { id = doc.DocId, mensagem = "Documento registrado." });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Atualizar(int id, [FromBody] CriarDocumentoRequest request)
        {
            var doc = await _context.VeiculosDocumentos.FindAsync(id);
            if (doc == null) return NotFound();
            if (!string.IsNullOrEmpty(request.Tipo) && !TiposValidos.Contains(request.Tipo.ToUpperInvariant()))
                return BadRequest(new { error = $"Tipo inválido. Use um destes: {string.Join(", ", TiposValidos)}." });
            if (!string.IsNullOrEmpty(request.Status) && !StatusValidos.Contains(request.Status.ToUpperInvariant()))
                return BadRequest(new { error = $"Status inválido. Use um destes: {string.Join(", ", StatusValidos)}." });

            doc.AtualizarDados(request.Tipo?.ToUpperInvariant(), request.Arquivo, request.Observacao, request.DataVencimento);
            if (!string.IsNullOrEmpty(request.Status)) doc.AlterarStatus(request.Status.ToUpperInvariant());

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> AtualizarStatus(int id, [FromBody] AtualizarDocumentoStatusRequest request)
        {
            var doc = await _context.VeiculosDocumentos.FindAsync(id);
            if (doc == null) return NotFound();
            if (string.IsNullOrEmpty(request.Status) || !StatusValidos.Contains(request.Status.ToUpperInvariant()))
                return BadRequest(new { error = $"Status inválido. Use um destes: {string.Join(", ", StatusValidos)}." });

            doc.AlterarStatus(request.Status.ToUpperInvariant());
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Excluir(int id)
        {
            var doc = await _context.VeiculosDocumentos.FindAsync(id);
            if (doc == null) return NotFound();
            _context.VeiculosDocumentos.Remove(doc);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CriarDocumentoRequest
    {
        public int VeiculoId { get; set; }
        public string Tipo { get; set; } = "";
        public string? Status { get; set; }
        public string? Arquivo { get; set; }
        public string? Observacao { get; set; }
        public DateTime? DataVencimento { get; set; }
    }

    public class AtualizarDocumentoStatusRequest
    {
        public string Status { get; set; } = "";
    }
}
