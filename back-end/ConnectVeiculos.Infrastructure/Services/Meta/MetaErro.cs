using System.Text.Json;

namespace ConnectVeiculos.Infrastructure.Services.Meta
{
    /// <summary>
    /// Traduz o erro da Graph API para o lojista. O token da Page nao expira,
    /// mas para de valer quando a pessoa troca a senha do Facebook, remove o app
    /// ou tira a permissao — e o que aparecia era o JSON cru da Meta.
    /// </summary>
    public static class MetaErro
    {
        public const string MensagemReconectar =
            "A conexão com a Meta parou de valer (senha do Facebook trocada, app removido ou permissão retirada). "
            + "Reconecte em Integrações → Meta.";

        public static string Descrever(int status, string body)
        {
            var (codigo, mensagem) = Ler(body);

            // 190 = token invalido/expirado/revogado; 102 = sessao do usuario.
            if (codigo is 190 or 102)
                return MensagemReconectar;

            // 10 e 200-299 = permissao que o app pediu e a pessoa nao concedeu.
            if (codigo == 10 || codigo is >= 200 and < 300)
                return "A Meta recusou por falta de permissão. Reconecte em Integrações → Meta e aceite todas as permissões.";

            return string.IsNullOrWhiteSpace(mensagem) ? $"Falha ({status}): {body}" : $"Falha na Meta: {mensagem}";
        }

        private static (int? codigo, string? mensagem) Ler(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("error", out var erro)) return (null, null);
                int? codigo = erro.TryGetProperty("code", out var c) && c.TryGetInt32(out var n) ? n : null;
                var mensagem = erro.TryGetProperty("message", out var m) ? m.GetString() : null;
                return (codigo, mensagem);
            }
            catch (JsonException)
            {
                return (null, null);
            }
        }
    }
}
