using ConnectVeiculos.Application.Interfaces.Imagens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImagensController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public ImagensController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpGet("veiculo/{veiculoId}")]
        public async Task<IActionResult> ConsultarImagens(
            [FromServices] IConsultarImagensVeiculoUseCase consultarImagensUseCase,
            int veiculoId)
        {
            var imagens = await consultarImagensUseCase.Execute(veiculoId);
            return Ok(imagens);
        }

        [HttpPost("veiculo/{veiculoId}")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadImagem(
            [FromServices] IUploadImagemVeiculoUseCase uploadImagemUseCase,
            int veiculoId,
            IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Arquivo nao enviado.");

            // Validar tipo de arquivo
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

            if (!extensoesPermitidas.Contains(extensao))
                return BadRequest("Tipo de arquivo nao permitido. Use JPG, PNG, GIF ou WEBP.");

            // Criar pasta se nao existir
            var uploadPath = Path.Combine(_environment.ContentRootPath, "uploads", "veiculos", veiculoId.ToString());
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            // Gerar nome unico para o arquivo
            var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
            var caminhoCompleto = Path.Combine(uploadPath, nomeArquivo);
            var caminhoRelativo = $"/uploads/veiculos/{veiculoId}/{nomeArquivo}";

            // Salvar arquivo
            using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
            {
                await arquivo.CopyToAsync(stream);
            }

            // Registrar no banco
            var imagem = await uploadImagemUseCase.Execute(veiculoId, caminhoRelativo);
            return CreatedAtAction(nameof(ConsultarImagens), new { veiculoId }, imagem);
        }

        [HttpDelete("{imagemId}")]
        public async Task<IActionResult> ExcluirImagem(
            [FromServices] IExcluirImagemVeiculoUseCase excluirImagemUseCase,
            int imagemId)
        {
            await excluirImagemUseCase.Execute(imagemId);
            return NoContent();
        }

        [AllowAnonymous]
        [HttpGet("file")]
        public IActionResult GetImageFile([FromQuery] string path)
        {
            if (string.IsNullOrEmpty(path))
                return NotFound();

            var filePath = Path.Combine(_environment.ContentRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var contentType = GetContentType(filePath);
            return PhysicalFile(filePath, contentType);
        }

        private static string GetContentType(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
    }
}
