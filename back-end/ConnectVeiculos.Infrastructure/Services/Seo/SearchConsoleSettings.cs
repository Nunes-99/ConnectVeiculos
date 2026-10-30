namespace ConnectVeiculos.Infrastructure.Services.Seo
{
    public class SearchConsoleSettings
    {
         // Credencial OAuth da conta Google dona da propriedade no Search Console
         // (a da plataforma, nao a de cada loja: o sitemap e' um so pra todas).
         // O refresh token precisa do escopo
         // https://www.googleapis.com/auth/webmasters (e siteverification, para o
         // cadastro automatico dos dominios das lojas). Qualquer um vazio
         // desliga o reenvio.
         public string ClientId { get; set; } = string.Empty;
         public string ClientSecret { get; set; } = string.Empty;
         public string RefreshToken { get; set; } = string.Empty;

         // Propriedade exatamente como aparece no Search Console: prefixo de URL
         // ("https://connectveiculos.dev.br/") ou dominio ("sc-domain:connectveiculos.dev.br").
         public string SiteUrl { get; set; } = string.Empty;

         // URL publica do site; o sitemap reenviado e' {PublicSiteUrl}/sitemap.xml.
         public string PublicSiteUrl { get; set; } = string.Empty;
    }
}
