using ConnectVeiculos.Core.Entities.Veiculos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConnectVeiculos.Infrastructure.Database.Interceptors
{
    /// <summary>
    /// Carimba VeiDtAtualizacao em todo veiculo criado ou alterado.
    ///
    /// Alimenta o &lt;lastmod&gt; do sitemap: e' o campo que o Google usa para
    /// decidir o que visitar de novo (changefreq e priority ele ignora). Fica no
    /// interceptor e nao nos use cases para nenhum caminho de alteracao — tela,
    /// importacao, venda, estorno, sincronizacao — esquecer de atualizar a data.
    /// </summary>
    public class VeiculoAtualizadoInterceptor : SaveChangesInterceptor
    {
        // Marcar "postado no Instagram/Facebook" nao muda nada na pagina publica
        // do veiculo; nao deve fazer o Google achar que ela mudou.
        private static readonly HashSet<string> CamposQueNaoMudamAPagina = new()
        {
            nameof(Veiculo.VeiPostadoInsta), nameof(Veiculo.VeiPostadoFace),
            nameof(Veiculo.VeiDtPostagemInsta), nameof(Veiculo.VeiDtPostagemFace),
        };

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context != null) Carimbar(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null) Carimbar(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void Carimbar(DbContext context)
        {
            var agora = DateTime.UtcNow;

            foreach (var entry in context.ChangeTracker.Entries<Veiculo>())
            {
                var mudouAPagina = entry.State == EntityState.Added
                    || (entry.State == EntityState.Modified
                        && entry.Properties.Any(p => p.IsModified
                                                     && p.Metadata.Name != nameof(Veiculo.VeiDtAtualizacao)
                                                     && !CamposQueNaoMudamAPagina.Contains(p.Metadata.Name)));

                if (mudouAPagina)
                    entry.Property(nameof(Veiculo.VeiDtAtualizacao)).CurrentValue = agora;
            }
        }
    }
}
