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

        // Limites do upload. Antes era [DisableRequestSizeLimit] +
        // MultipartBodyLengthLimit = long.MaxValue: qualquer usuario autenticado
        // podia encher o disco da VM (Free Tier tem pouco espaco).
        private const long TamanhoMaximoBytes = 15 * 1024 * 1024; // 15 MB
        private const int LadoMaximoPx = 1920;

        [HttpPost("veiculo/{veiculoId}")]
        [RequestSizeLimit(TamanhoMaximoBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = TamanhoMaximoBytes)]
        public async Task<IActionResult> UploadImagem(
            [FromServices] IUploadImagemVeiculoUseCase uploadImagemUseCase,
            int veiculoId,
            IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Arquivo não enviado.");

            if (arquivo.Length > TamanhoMaximoBytes)
                return BadRequest($"Arquivo muito grande. Maximo {TamanhoMaximoBytes / (1024 * 1024)} MB.");

            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

            if (!extensoesPermitidas.Contains(extensao))
                return BadRequest("Tipo de arquivo não permitido. Use JPG, PNG, GIF ou WEBP.");

            var uploadPath = Path.Combine(_environment.ContentRootPath, "uploads", "veiculos", veiculoId.ToString());
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            string caminhoRelativo;

            try
            {
                // Decodificar com ImageSharp faz as vezes de validacao de conteudo:
                // extensao .png num executavel/HTML nao passa daqui. Antes a checagem
                // era so pela extensao do nome do arquivo.
                using var image = await Image.LoadAsync(arquivo.OpenReadStream());

                // Redimensiona o que vier maior que LadoMaximoPx. Foto de celular
                // tem 4000px+ e ia inteira pro catalogo publico.
                if (image.Width > LadoMaximoPx || image.Height > LadoMaximoPx)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(LadoMaximoPx, LadoMaximoPx)
                    }));
                }

                // Normaliza tudo pra JPEG: formato unico simplifica o catalogo e
                // corta metadados (inclusive GPS da foto original).
                var nomeArquivo = $"{Guid.NewGuid()}.jpg";
                var caminhoCompleto = Path.Combine(uploadPath, nomeArquivo);
                caminhoRelativo = $"/uploads/veiculos/{veiculoId}/{nomeArquivo}";

                await image.SaveAsJpegAsync(caminhoCompleto, new JpegEncoder { Quality = 85 });
            }
            catch (UnknownImageFormatException)
            {
                return BadRequest("Arquivo não é uma imagem válida.");
            }
            catch (InvalidImageContentException)
            {
                return BadRequest("Imagem corrompida ou ilegível.");
            }

            var imagem = await uploadImagemUseCase.Execute(veiculoId, caminhoRelativo);
            return CreatedAtAction(nameof(ConsultarImagens), new { veiculoId }, imagem);
        }

        /// <summary>
        /// Upload de imagem da loja (banner do catalogo e favicon). Diferente do
        /// upload de veiculo, nao grava linha em tabela: devolve o caminho e quem
        /// chama guarda no campo correspondente da Loja.
        ///
        /// O logo da loja continua indo em base64 dentro do proprio registro. Aqui
        /// nao da: o banner e' grande e viajaria em toda resposta do catalogo
        /// publico, que e' a rota mais quente do sistema.
        /// </summary>
        /// <param name="lojaId">Loja dona do arquivo.</param>
        /// <param name="tipo">"banner" ou "favicon" — define o tamanho maximo.</param>
        /// <param name="arquivo">Imagem enviada.</param>
        /// <response code="200">Caminho relativo do arquivo gravado.</response>
        /// <response code="400">Arquivo ausente, grande demais, tipo invalido ou corrompido.</response>
        [HttpPost("loja/{lojaId}")]
        [RequestSizeLimit(TamanhoMaximoBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = TamanhoMaximoBytes)]
        public async Task<IActionResult> UploadImagemLoja(
            int lojaId,
            [FromQuery] string tipo,
            IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Arquivo não enviado.");

            if (arquivo.Length > TamanhoMaximoBytes)
                return BadRequest($"Arquivo muito grande. Maximo {TamanhoMaximoBytes / (1024 * 1024)} MB.");

            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

            if (!extensoesPermitidas.Contains(extensao))
                return BadRequest("Tipo de arquivo não permitido. Use JPG, PNG, GIF ou WEBP.");

            var ehFavicon = string.Equals(tipo, "favicon", StringComparison.OrdinalIgnoreCase);
            var nomeTipo = ehFavicon ? "favicon" : "banner";
            // Favicon aparece em 32px; guardar 1920 seria desperdicio de banda.
            var ladoMaximo = ehFavicon ? 256 : LadoMaximoPx;

            var uploadPath = Path.Combine(_environment.ContentRootPath, "uploads", "lojas", lojaId.ToString());
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            string caminhoRelativo;

            try
            {
                using var image = await Image.LoadAsync(arquivo.OpenReadStream());

                if (image.Width > ladoMaximo || image.Height > ladoMaximo)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(ladoMaximo, ladoMaximo)
                    }));
                }

                // PNG no favicon preserva transparencia; o banner vai a JPEG, que
                // fica bem menor numa foto de fundo.
                var nomeArquivo = ehFavicon ? $"{nomeTipo}-{Guid.NewGuid()}.png" : $"{nomeTipo}-{Guid.NewGuid()}.jpg";
                var caminhoCompleto = Path.Combine(uploadPath, nomeArquivo);
                caminhoRelativo = $"/uploads/lojas/{lojaId}/{nomeArquivo}";

                if (ehFavicon)
                    await image.SaveAsPngAsync(caminhoCompleto);
                else
                    await image.SaveAsJpegAsync(caminhoCompleto, new JpegEncoder { Quality = 85 });
            }
            catch (UnknownImageFormatException)
            {
                return BadRequest("Arquivo não é uma imagem válida.");
            }
            catch (InvalidImageContentException)
            {
                return BadRequest("Imagem corrompida ou ilegível.");
            }

            return Ok(new { caminho = caminhoRelativo });
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
        // VaryByQueryKeys e obrigatorio: sem ele o ResponseCaching usa so o path da rota
        // como chave e TODAS as imagens passam a servir a primeira resposta cacheada.
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any,
                       VaryByQueryKeys = new[] { "path", "max", "format" })]
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
