namespace ConnectVeiculos.Core.Catalogo
{
    /// <summary>
    /// Fonte unica das URLs publicas do catalogo.
    ///
    /// A rota do Angular e' <c>/catalogo/{tenantSlug}/veiculo/{id}</c> — o slug e'
    /// do TENANT, nao da loja. Isso ja foi errado de cinco jeitos diferentes
    /// (slug da loja, id da loja, e sem slug nenhum) e o sintoma e' silencioso:
    /// o router nao acha a rota, cai no wildcard e joga o visitante na landing
    /// page. Ninguem percebe ate um cliente reclamar que o anuncio "nao abre".
    ///
    /// Qualquer lugar que precise linkar um veiculo publicamente — feeds, QR code,
    /// e-mail, publicacao no Facebook/Google — deve passar por aqui.
    /// </summary>
    public static class CatalogoUrl
    {
        /// <summary>
        /// URL publica de um veiculo. Sem <paramref name="tenantSlug"/> devolve a
        /// raiz do catalogo (que ainda resolve por subdominio) em vez de montar um
        /// link que nao abre nada.
        /// </summary>
        public static string Veiculo(string baseUrl, string tenantSlug, int veiculoId)
        {
            var raiz = Normalizar(baseUrl);

            return string.IsNullOrWhiteSpace(tenantSlug)
                ? $"{raiz}/catalogo"
                : $"{raiz}/catalogo/{Uri.EscapeDataString(tenantSlug.Trim())}/veiculo/{veiculoId}";
        }

        /// <summary>URL publica da listagem do catalogo de um tenant.</summary>
        public static string Listagem(string baseUrl, string tenantSlug)
        {
            var raiz = Normalizar(baseUrl);

            return string.IsNullOrWhiteSpace(tenantSlug)
                ? $"{raiz}/catalogo"
                : $"{raiz}/catalogo/{Uri.EscapeDataString(tenantSlug.Trim())}";
        }

        private static string Normalizar(string baseUrl)
            => (baseUrl ?? string.Empty).Trim().TrimEnd('/');
    }
}
