using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Stories
{
    /// <summary>
    /// Artes geradas para a Meta baixar (stories e capa do post). Ficam onde o
    /// /api/imagens/file ja serve arquivos e so precisam existir ate a Meta
    /// buscar: dois dias de folga cobrem uma nova tentativa manual sem deixar a
    /// pasta crescer para sempre.
    /// </summary>
    public static class ArtesTemporarias
    {
        private static readonly TimeSpan Validade = TimeSpan.FromDays(2);

        /// <summary>Grava a arte, apaga as vencidas e devolve a URL publica.</summary>
        public static async Task<string> GravarAsync(string contentRoot, byte[] jpeg, string baseUrl, ILogger logger)
        {
            var pasta = Path.Combine(contentRoot, "uploads", "_stories");
            Directory.CreateDirectory(pasta);

            try
            {
                foreach (var antigo in Directory.EnumerateFiles(pasta, "*.jpg"))
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(antigo) > Validade)
                        File.Delete(antigo);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Falha ao limpar artes antigas");
            }

            var nome = $"{Guid.NewGuid():N}.jpg";
            await File.WriteAllBytesAsync(Path.Combine(pasta, nome), jpeg);
            return $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString($"/uploads/_stories/{nome}")}";
        }

        /// <summary>Caminho no disco de um caminho relativo como "/uploads/x/y.jpg".</summary>
        public static string NoDisco(string contentRoot, string caminhoRelativo)
            => Path.Combine(contentRoot, caminhoRelativo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        public static string? NormalizarBaseUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                if (!Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out uri))
                    return null;
            }
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            if (string.IsNullOrWhiteSpace(uri.Host)) return null;
            var port = uri.IsDefaultPort ? "" : ":" + uri.Port;
            return $"{uri.Scheme}://{uri.Host}{port}";
        }
    }
}
