using ConnectVeiculos.Core.Entities.Acessos;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Logs;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace ConnectVeiculos.Infrastructure.Database.Interceptors
{
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private bool _isSavingAudit = false;

        // Entidades que devem ser auditadas
        private static readonly HashSet<Type> _auditedTypes = new()
        {
            typeof(Usuario),
            typeof(Veiculo),
            typeof(Venda),
            typeof(Loja),
            typeof(Categoria),
            typeof(Acesso)
        };

        // Mapeamento tipo -> nome da tabela
        private static readonly Dictionary<Type, string> _tableNames = new()
        {
            { typeof(Usuario), "Usuario" },
            { typeof(Veiculo), "Veiculo" },
            { typeof(Venda), "Venda" },
            { typeof(Loja), "Loja" },
            { typeof(Categoria), "Categoria" },
            { typeof(Acesso), "Acesso" }
        };

        public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (_isSavingAudit || eventData.Context == null)
                return base.SavingChanges(eventData, result);

            var auditEntries = GetAuditEntries(eventData.Context);
            var saveResult = base.SavingChanges(eventData, result);
            SaveAuditLogs(eventData.Context, auditEntries);
            return saveResult;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_isSavingAudit || eventData.Context == null)
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            var auditEntries = GetAuditEntries(eventData.Context);
            var saveResult = await base.SavingChangesAsync(eventData, result, cancellationToken);
            await SaveAuditLogsAsync(eventData.Context, auditEntries, cancellationToken);
            return saveResult;
        }

        private List<AuditEntry> GetAuditEntries(DbContext context)
        {
            var auditEntries = new List<AuditEntry>();
            var httpContext = _httpContextAccessor.HttpContext;

            int? usuarioId = null;
            string? usuarioNome = null;
            string? ip = null;

            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirst("sub");
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid))
                    usuarioId = uid;

                usuarioNome = httpContext.User.FindFirst(ClaimTypes.Name)?.Value
                    ?? httpContext.User.FindFirst("name")?.Value;

                ip = httpContext.Connection.RemoteIpAddress?.ToString();
            }

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (!_auditedTypes.Contains(entry.Entity.GetType()))
                    continue;

                if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var tableName = _tableNames[entry.Entity.GetType()];

                auditEntries.Add(new AuditEntry
                {
                    Entry = entry,
                    TableName = tableName,
                    State = entry.State,
                    UsuarioId = usuarioId,
                    UsuarioNome = usuarioNome,
                    IP = ip,
                    OldValues = GetValues(entry, entry.State == EntityState.Added ? null : entry.Properties.Where(p => entry.State != EntityState.Modified || p.IsModified || p.Metadata.IsPrimaryKey())),
                    NewValues = GetValues(entry, entry.State == EntityState.Deleted ? null : entry.Properties)
                });
            }

            return auditEntries;
        }

        private static Dictionary<string, object?>? GetValues(EntityEntry entry, IEnumerable<PropertyEntry>? properties)
        {
            if (properties == null) return null;

            var values = new Dictionary<string, object?>();
            foreach (var prop in properties)
            {
                var value = prop.CurrentValue;
                // Não incluir senhas nos logs
                if (prop.Metadata.Name.Contains("Senha", StringComparison.OrdinalIgnoreCase)
                    || prop.Metadata.Name.Contains("Password", StringComparison.OrdinalIgnoreCase))
                {
                    value = "***";
                }
                values[prop.Metadata.Name] = value;
            }
            return values;
        }

        private static int? GetPrimaryKeyValue(EntityEntry entry)
        {
            var pk = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            if (pk?.CurrentValue is int id) return id;
            return null;
        }

        private void SaveAuditLogs(DbContext context, List<AuditEntry> auditEntries)
        {
            if (auditEntries.Count == 0) return;

            foreach (var auditEntry in auditEntries)
            {
                var registroId = GetPrimaryKeyValue(auditEntry.Entry);
                var log = CreateLog(auditEntry, registroId);
                if (log != null)
                    context.Set<LogAuditoria>().Add(log);
            }

            _isSavingAudit = true;
            try
            {
                context.SaveChanges();
            }
            finally
            {
                _isSavingAudit = false;
            }
        }

        private async Task SaveAuditLogsAsync(DbContext context, List<AuditEntry> auditEntries, CancellationToken cancellationToken)
        {
            if (auditEntries.Count == 0) return;

            foreach (var auditEntry in auditEntries)
            {
                var registroId = GetPrimaryKeyValue(auditEntry.Entry);
                var log = CreateLog(auditEntry, registroId);
                if (log != null)
                    context.Set<LogAuditoria>().Add(log);
            }

            _isSavingAudit = true;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _isSavingAudit = false;
            }
        }

        private static LogAuditoria? CreateLog(AuditEntry auditEntry, int? registroId)
        {
            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };

            return auditEntry.State switch
            {
                EntityState.Added => new LogAuditoria(
                    tabela: auditEntry.TableName,
                    acao: "INSERT",
                    registroId: registroId,
                    dadosAntigos: null,
                    dadosNovos: auditEntry.NewValues != null ? JsonSerializer.Serialize(auditEntry.NewValues, jsonOptions) : null,
                    usuarioId: auditEntry.UsuarioId,
                    usuarioNome: auditEntry.UsuarioNome,
                    ip: auditEntry.IP
                ),
                EntityState.Modified => new LogAuditoria(
                    tabela: auditEntry.TableName,
                    acao: "UPDATE",
                    registroId: registroId,
                    dadosAntigos: auditEntry.OldValues != null ? JsonSerializer.Serialize(auditEntry.OldValues, jsonOptions) : null,
                    dadosNovos: auditEntry.NewValues != null ? JsonSerializer.Serialize(auditEntry.NewValues, jsonOptions) : null,
                    usuarioId: auditEntry.UsuarioId,
                    usuarioNome: auditEntry.UsuarioNome,
                    ip: auditEntry.IP
                ),
                EntityState.Deleted => new LogAuditoria(
                    tabela: auditEntry.TableName,
                    acao: "DELETE",
                    registroId: registroId,
                    dadosAntigos: auditEntry.OldValues != null ? JsonSerializer.Serialize(auditEntry.OldValues, jsonOptions) : null,
                    dadosNovos: null,
                    usuarioId: auditEntry.UsuarioId,
                    usuarioNome: auditEntry.UsuarioNome,
                    ip: auditEntry.IP
                ),
                _ => null
            };
        }

        private class AuditEntry
        {
            public EntityEntry Entry { get; set; } = null!;
            public string TableName { get; set; } = string.Empty;
            public EntityState State { get; set; }
            public int? UsuarioId { get; set; }
            public string? UsuarioNome { get; set; }
            public string? IP { get; set; }
            public Dictionary<string, object?>? OldValues { get; set; }
            public Dictionary<string, object?>? NewValues { get; set; }
        }
    }
}
