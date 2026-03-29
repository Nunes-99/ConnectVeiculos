using ConnectVeiculos.Core.Entities.Favoritos;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FavoritosController : ControllerBase
    {
        private readonly ConnectVeiculosDbContext _context;

        public FavoritosController(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Registrar favorito (publico) - visitante informa e-mail para favoritar
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Favoritar([FromBody] FavoritarRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("E-mail e obrigatorio para favoritar.");

            // Verificar se ja favoritou este veiculo
            var existente = await _context.Favoritos
                .FirstOrDefaultAsync(f => f.FavEmail == request.Email && f.R_VeiId == request.VeiculoId);

            if (existente != null)
                return Ok(new { id = existente.FavId, mensagem = "Veiculo ja esta nos favoritos." });

            var favorito = new Favorito(0, request.VeiculoId, request.Email, request.Nome, request.Telefone);
            _context.Favoritos.Add(favorito);
            await _context.SaveChangesAsync();

            return Ok(new { id = favorito.FavId, mensagem = "Veiculo adicionado aos favoritos!" });
        }

        /// <summary>
        /// Remover favorito (publico)
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> Desfavoritar([FromQuery] string email, [FromQuery] int veiculoId)
        {
            var favorito = await _context.Favoritos
                .FirstOrDefaultAsync(f => f.FavEmail == email && f.R_VeiId == veiculoId);

            if (favorito == null) return NotFound();

            _context.Favoritos.Remove(favorito);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Listar favoritos de um visitante por e-mail (publico)
        /// </summary>
        [HttpGet("meus")]
        public async Task<IActionResult> MeusFavoritos([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("E-mail e obrigatorio.");

            var favoritos = await _context.Favoritos
                .Where(f => f.FavEmail == email)
                .OrderByDescending(f => f.FavDtCriacao)
                .Select(f => f.R_VeiId)
                .ToListAsync();

            return Ok(favoritos);
        }

        /// <summary>
        /// Relatorio de veiculos mais favoritados (autenticado - admin)
        /// </summary>
        [HttpGet("relatorio")]
        [Authorize]
        public async Task<IActionResult> Relatorio()
        {
            var relatorio = await _context.Favoritos
                .GroupBy(f => f.R_VeiId)
                .Select(g => new
                {
                    veiculoId = g.Key,
                    totalFavoritos = g.Count(),
                    ultimoFavorito = g.Max(f => f.FavDtCriacao)
                })
                .OrderByDescending(r => r.totalFavoritos)
                .ToListAsync();

            // Enriquecer com dados do veiculo
            var veiculoIds = relatorio.Select(r => r.veiculoId).ToList();
            var veiculos = await _context.Veiculos
                .Where(v => veiculoIds.Contains(v.VeiId))
                .ToListAsync();

            var resultado = relatorio.Select(r =>
            {
                var vei = veiculos.FirstOrDefault(v => v.VeiId == r.veiculoId);
                return new
                {
                    r.veiculoId,
                    marca = vei?.VeiMarca ?? "",
                    modelo = vei?.VeiModelo ?? "",
                    ano = vei?.VeiAno ?? 0,
                    preco = vei?.VeiPreco ?? 0,
                    status = vei?.VeiSts ?? "",
                    r.totalFavoritos,
                    r.ultimoFavorito
                };
            }).ToList();

            return Ok(resultado);
        }

        /// <summary>
        /// Lista de visitantes que favoritaram (autenticado - admin)
        /// </summary>
        [HttpGet("visitantes")]
        [Authorize]
        public async Task<IActionResult> Visitantes([FromQuery] int? veiculoId = null)
        {
            var query = _context.Favoritos.AsQueryable();
            if (veiculoId.HasValue)
                query = query.Where(f => f.R_VeiId == veiculoId.Value);

            var visitantes = await query
                .GroupBy(f => f.FavEmail)
                .Select(g => new
                {
                    email = g.Key,
                    nome = g.First().FavNome,
                    telefone = g.First().FavTelefone,
                    totalFavoritos = g.Count(),
                    ultimaAtividade = g.Max(f => f.FavDtCriacao)
                })
                .OrderByDescending(v => v.ultimaAtividade)
                .ToListAsync();

            return Ok(visitantes);
        }
    }

    public class FavoritarRequest
    {
        public int VeiculoId { get; set; }
        public string Email { get; set; }
        public string Nome { get; set; }
        public string Telefone { get; set; }
    }
}
