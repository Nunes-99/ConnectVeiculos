using System.Collections.Concurrent;
using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <summary>
    /// Cache em memoria do registry de tenants. Singleton — invalidado quando
    /// criar/atualizar/suspender tenant via script de onboarding.
    /// Como o conjunto de tenants muda raramente (criar tenant novo eh evento),
    /// cache simples sem TTL eh adequado.
    /// </summary>
    internal sealed class TenantStore : ITenantStore
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ConcurrentDictionary<string, Tenant> _bySlug = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<int, Tenant> _byId = new();
        private volatile bool _loaded;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public TenantStore(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        {
            await EnsureLoadedAsync(ct);
            return _bySlug.TryGetValue(slug, out var t) ? t : null;
        }

        public async Task<Tenant?> GetByIdAsync(int tenantId, CancellationToken ct = default)
        {
            await EnsureLoadedAsync(ct);
            return _byId.TryGetValue(tenantId, out var t) ? t : null;
        }

        public async Task<IReadOnlyList<Tenant>> ListActiveAsync(CancellationToken ct = default)
        {
            await EnsureLoadedAsync(ct);
            return _byId.Values.Where(t => t.TenStatus == TenantStatus.Active).ToList();
        }

        public void InvalidateCache()
        {
            _loaded = false;
            _bySlug.Clear();
            _byId.Clear();
        }

        public async Task UpdateVerificationCodesAsync(int tenantId, string? googleCode, string? facebookCode, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var master = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

            var tenant = await master.Tenants.FirstOrDefaultAsync(x => x.TenId == tenantId, ct)
                ?? throw new InvalidOperationException($"Tenant {tenantId} nao encontrado.");

            // null = nao alterar; string vazia = limpar; valor = setar.
            if (googleCode != null) tenant.SetGoogleVerifCode(googleCode);
            if (facebookCode != null) tenant.SetFacebookVerifCode(facebookCode);

            await master.SaveChangesAsync(ct);

            // Atualiza o cache in-place sem precisar invalidar tudo.
            _bySlug[tenant.TenSlug] = tenant;
            _byId[tenant.TenId] = tenant;
        }

        private async Task EnsureLoadedAsync(CancellationToken ct)
        {
            if (_loaded) return;
            await _loadLock.WaitAsync(ct);
            try
            {
                if (_loaded) return;

                using var scope = _scopeFactory.CreateScope();
                var master = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
                // EnsureCreated cobre o caso de primeiro boot — banco master ainda nao existe.
                await master.Database.EnsureCreatedAsync(ct);

                var tenants = await master.Tenants.AsNoTracking().ToListAsync(ct);
                foreach (var t in tenants)
                {
                    _bySlug[t.TenSlug] = t;
                    _byId[t.TenId] = t;
                }
                _loaded = true;
            }
            finally
            {
                _loadLock.Release();
            }
        }
    }
}
