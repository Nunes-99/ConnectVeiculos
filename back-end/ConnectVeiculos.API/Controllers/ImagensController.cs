using ConnectVeiculos.Application.Interfaces.Imagens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

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
                return BadRequest("Arquivo não enviado.");

            // Validar tipo de arquivo
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

            if (!extensoesPermitidas.Contains(extensao))
                return BadRequest("Tipo de arquivo não permitido. Use JPG, PNG, GIF ou WEBP.");

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

        [HttpPut("{imagemId}/principal")]
        public async Task<IActionResult> DefinirPrincipal(
            [FromServices] IDefinirImagemPrincipalUseCase definirPrincipalUseCase,
            int imagemId)
        {
            await definirPrincipalUseCase.Execute(imagemId);
            return NoContent();
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
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GetImageFile(
            [FromQuery] string path,
            [FromQuery] int? max = null,
            [FromQuery] string? format = null)
        {
            if (string.IsNullOrEmpty(path))
                return NotFound();

            var filePath = Path.Combine(_environment.ContentRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            // Sem transformacao: serve direto (path do disco, zero alocacao).
            // Default path usado por catalogo publico, ML, Google etc.
            if (!max.HasValue && string.IsNullOrEmpty(format))
                return PhysicalFile(filePath, GetContentType(filePath));

            // Com transformacao: usado pelo Instagram/Facebook Page que precisam
            // de JPEG <= 8MB e lado maximo 1440px. Sanitiza inputs antes.
            var alvo = Math.Clamp(max ?? 1440, 100, 2048);
            var fmt = (format ?? "jpeg").ToLowerInvariant();

            try
            {
                using var image = await Image.LoadAsync(filePath);
                if (image.Width > alvo || image.Height > alvo)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(alvo, alvo)
                    }));
                }

                var ms = new MemoryStream();
                if (fmt == "jpeg" || fmt == "jpg")
                {
                    // Quality 85 da boa relacao qualidade/tamanho. IG limita 8MB;
                    // mesmo fotos grandes ficam <2MB nesse setup.
                    await image.SaveAsync(ms, new JpegEncoder { Quality = 85 });
                    ms.Position = 0;
                    return File(ms, "image/jpeg");
                }

                // Fallback: serve original se format desconhecido.
                ms.Dispose();
                return PhysicalFile(filePath, GetContentType(filePath));
            }
            catch
            {
                // Se decodificacao falhar (arquivo corrompido), cai pro original.
                return PhysicalFile(filePath, GetContentType(filePath));
            }
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
