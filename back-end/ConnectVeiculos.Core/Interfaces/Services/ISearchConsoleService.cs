namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface ISearchConsoleService
    {
         // Reenvia o sitemap.xml da plataforma ao Google Search Console, pra ele
         // reler o <lastmod> dos veiculos sem esperar a proxima visita. Chamar
         // fire-and-forget apos CRUD de veiculo; desligado sem as credenciais.
         Task ReenviarSitemapAsync(CancellationToken ct = default);
    }
}
