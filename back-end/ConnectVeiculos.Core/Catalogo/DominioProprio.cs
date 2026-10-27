using System.Globalization;
using System.Text.RegularExpressions;

namespace ConnectVeiculos.Core.Catalogo
{
    /// <summary>
    /// Regras do dominio proprio do catalogo (ex: diamanteveiculos.com.br).
    ///
    /// O dominio e' guardado sempre na forma "nua": minusculo, sem protocolo,
    /// sem caminho, sem porta e sem "www." — o www e' aceito na entrada e no
    /// Host da requisicao, mas os dois apontam para o mesmo tenant.
    /// </summary>
    public static class DominioProprio
    {
        // Rotulos de 1 a 63 caracteres, sem hifen nas pontas; TLD so de letras
        // (ou xn-- para dominio internacionalizado ja convertido).
        private static readonly Regex Formato = new(
            @"^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+([a-z]{2,63}|xn--[a-z0-9-]{1,59})$",
            RegexOptions.Compiled);

        // Dominios da propria plataforma: ja resolvem o tenant por subdominio.
        private static readonly string[] DaPlataforma =
        {
            "connectveiculos.dev.br",
            "connectveiculos.com.br",
        };

        /// <summary>
        /// Converte o que o lojista digitou (ou o Host da requisicao) para a forma
        /// guardada. Aceita "https://www.Loja.com.br/catalogo", "loja.com.br:443"
        /// etc. Devolve null para entrada vazia.
        /// </summary>
        public static string? Normalizar(string? entrada)
        {
            var s = (entrada ?? string.Empty).Trim().ToLowerInvariant();
            if (s.Length == 0) return null;

            var esquema = s.IndexOf("://", StringComparison.Ordinal);
            if (esquema >= 0) s = s[(esquema + 3)..];

            var fim = s.IndexOfAny(new[] { '/', '?', '#' });
            if (fim >= 0) s = s[..fim];

            var porta = s.LastIndexOf(':');
            if (porta >= 0) s = s[..porta];

            s = s.TrimEnd('.');
            if (s.StartsWith("www.", StringComparison.Ordinal)) s = s[4..];

            // Dominio com acento (ex: veículos.com.br) vira punycode, que e' o que
            // chega no Host e o que o certbot entende.
            try { s = new IdnMapping().GetAscii(s); }
            catch (ArgumentException) { /* fica como esta; EhValido rejeita */ }

            return s.Length == 0 ? null : s;
        }

        /// <summary>Formato de nome de dominio com pelo menos um ponto.</summary>
        public static bool EhValido(string? dominio)
            => !string.IsNullOrEmpty(dominio) && Formato.IsMatch(dominio);

        /// <summary>
        /// True para connectveiculos.dev.br e qualquer subdominio dele — esses
        /// nao podem ser cadastrados como dominio proprio de uma loja.
        /// </summary>
        public static bool EhDaPlataforma(string dominio)
            => DaPlataforma.Any(r => dominio == r || dominio.EndsWith("." + r, StringComparison.Ordinal));

        /// <summary>URL base usada nos links publicos quando o dominio esta ativo.</summary>
        public static string UrlBase(string dominio) => $"https://{dominio}";
    }
}
