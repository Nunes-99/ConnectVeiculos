namespace ConnectVeiculos.Infrastructure.Services.Seo
{
    public class IndexNowSettings
    {
         // IndexNow é um protocolo aberto suportado por Bing, Yandex, DuckDuckGo
         // e Seznam. Notifica os buscadores na hora que uma URL muda, sem
         // depender de re-crawl periódico. Google NÃO suporta — pra ele a
         // indexação continua via sitemap + Search Console.

         // Enabled=false desativa todas as chamadas (útil em dev local).
         public bool Enabled { get; set; }

         // Chave de 8-128 caracteres hex. Precisa ser o mesmo valor exposto
         // em https://{host}/{Key}.txt como prova de propriedade do domínio.
         public string Key { get; set; } = string.Empty;

         // URL base pública do site (usada pra montar /catalogo/{slug}/veiculo/{id}
         // e o keyLocation). Em dev: localhost; em prod: connectveiculos.dev.br.
         public string PublicSiteUrl { get; set; } = string.Empty;
    }
}
