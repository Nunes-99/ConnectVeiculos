namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface ISearchConsoleService
    {
         // Reenvia o sitemap.xml da plataforma ao Google Search Console, pra ele
         // reler o <lastmod> dos veiculos sem esperar a proxima visita. Chamar
         // fire-and-forget apos CRUD de veiculo; desligado sem as credenciais.
         Task ReenviarSitemapAsync(CancellationToken ct = default);

         // Dominio proprio de loja no Search Console da plataforma. siteUrl no
         // formato de prefixo ("https://loja.com.br/"). O refresh token precisa
         // tambem do escopo siteverification. Desligado sem as credenciais.
         bool CadastroDeDominiosLigado { get; }

         // Codigo (o "content" da meta tag) que o Google quer ver no <head> do site.
         Task<string?> PedirCodigoVerificacaoAsync(string siteUrl, CancellationToken ct = default);

         // O codigo ja aparece na pagina inicial do site?
         Task<bool> CodigoNoArAsync(string siteUrl, string codigo, CancellationToken ct = default);

         // Verifica a propriedade (meta tag), adiciona ao Search Console e envia o sitemap.
         Task<bool> CadastrarSiteAsync(string siteUrl, CancellationToken ct = default);
    }
}
