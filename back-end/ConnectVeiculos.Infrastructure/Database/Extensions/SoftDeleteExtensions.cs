using ConnectVeiculos.Core.Interfaces.Common;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ConnectVeiculos.Infrastructure.Database.Extensions
{
    /// <summary>
    /// Extensoes para aplicar filtros globais de soft delete
    /// </summary>
    public static class SoftDeleteExtensions
    {
        /// <summary>
        /// Aplica filtro global de soft delete para todas as entidades que implementam ISoftDeletable
        /// </summary>
        public static void ApplySoftDeleteQueryFilters(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.Property(parameter, nameof(ISoftDeletable.Excluido));
                    var filter = Expression.Lambda(
                        Expression.Equal(property, Expression.Constant(false)),
                        parameter);

                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
                }
            }
        }

        /// <summary>
        /// Configura as propriedades de soft delete para uma entidade
        /// </summary>
        public static void ConfigureSoftDelete<T>(this ModelBuilder modelBuilder) where T : class, ISoftDeletable
        {
            modelBuilder.Entity<T>(entity =>
            {
                entity.Property(e => e.Excluido).HasDefaultValue(false);
                entity.Property(e => e.ExcluidoEm);
                entity.HasQueryFilter(e => !e.Excluido);
            });
        }

        /// <summary>
        /// Extensao para incluir registros excluidos nas consultas
        /// </summary>
        public static IQueryable<T> IncluirExcluidos<T>(this IQueryable<T> query) where T : class, ISoftDeletable
        {
            return query.IgnoreQueryFilters();
        }

        /// <summary>
        /// Extensao para obter apenas registros excluidos
        /// </summary>
        public static IQueryable<T> ApenasExcluidos<T>(this IQueryable<T> query) where T : class, ISoftDeletable
        {
            return query.IgnoreQueryFilters().Where(e => e.Excluido);
        }
    }
}
