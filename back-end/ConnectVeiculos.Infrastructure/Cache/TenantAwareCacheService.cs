using ConnectVeiculos.Core.Interfaces.Tenancy;

namespace ConnectVeiculos.Infrastructure.Cache
{
    /// <summary>
    /// Decorator do ICacheService que prefixa toda chave com "tenant:{id}:" quando
    /// ha um tenant resolvido na request. Sem isso, dois tenants compartilhariam
    /// chaves identicas (ex: "dashboard") e poderia haver vazamento de dados.
    ///
    /// Quando ITenantContext.IsResolved=false (ex: jobs de background sem tenant
    /// resolvido manualmente, ou chamadas em startup), as chaves nao recebem prefix
    /// — comportamento de "cache global". So use cache fora de tenant context para
    /// dados publicos/compartilhados (ex: cache da API FIPE).
    /// </summary>
    public sealed class TenantAwareCacheService : ICacheService
    {
        private readonly MemoryCacheService _inner;
        private readonly ITenantContext _tenant;

        public TenantAwareCacheService(MemoryCacheService inner, ITenantContext tenant)
        {
            _inner = inner;
            _tenant = tenant;
        }

        private string Scope(string key) =>
            _tenant.IsResolved ? $"tenant:{_tenant.TenantId}:{key}" : key;

        public T? Get<T>(string key) => _inner.Get<T>(Scope(key));
        public Task<T?> GetAsync<T>(string key) => _inner.GetAsync<T>(Scope(key));

        public void Set<T>(string key, T value, TimeSpan? expiration = null) =>
            _inner.Set(Scope(key), value, expiration);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) =>
            _inner.SetAsync(Scope(key), value, expiration);

        public void Remove(string key) => _inner.Remove(Scope(key));
        public Task RemoveAsync(string key) => _inner.RemoveAsync(Scope(key));

        public void RemoveByPrefix(string prefix) => _inner.RemoveByPrefix(Scope(prefix));

        public bool Exists(string key) => _inner.Exists(Scope(key));
        public Task<bool> ExistsAsync(string key) => _inner.ExistsAsync(Scope(key));
    }
}
