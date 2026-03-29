using ConnectVeiculos.Core.Interfaces.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConnectVeiculos.Infrastructure.Database.Interceptors
{
    /// <summary>
    /// Interceptor que converte operacoes de Delete em Soft Delete
    /// para entidades que implementam ISoftDeletable
    /// </summary>
    public class SoftDeleteInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (eventData.Context != null)
            {
                ProcessSoftDelete(eventData.Context);
            }
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
            {
                ProcessSoftDelete(eventData.Context);
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void ProcessSoftDelete(DbContext context)
        {
            var entries = context.ChangeTracker
                .Entries<ISoftDeletable>()
                .Where(e => e.State == EntityState.Deleted);

            foreach (var entry in entries)
            {
                // Alterar o estado de Deleted para Modified
                entry.State = EntityState.Modified;

                // Marcar como excluido logicamente
                entry.Entity.Excluir();
            }
        }
    }
}
