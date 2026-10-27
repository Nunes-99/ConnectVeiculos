using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <summary>
    /// Os links publicos (feeds, posts no Facebook/Instagram, Google Merchant,
    /// e-mails) usam Loja.LojUrlCatalogo como base. Quando o dominio proprio do
    /// tenant fica ativo, todas as lojas dele passam a apontar para o dominio;
    /// quando e' removido, voltam para o endereco da plataforma.
    /// </summary>
    public static class DominioProprioLojas
    {
        public static Task<int> AplicarUrlCatalogoAsync(ConnectVeiculosDbContext ctx, string urlBase, CancellationToken ct = default)
            => ctx.Database.ExecuteSqlRawAsync("UPDATE Loja SET LojUrlCatalogo = {0}", new object[] { urlBase }, ct);
    }
}
