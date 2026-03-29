using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ConnectVeiculos.Infrastructure.Cache
{
    /// <summary>
    /// Implementacao de cache distribuido com Redis
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(10);

        public RedisCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public T Get<T>(string key)
        {
            var data = _cache.GetString(key);
            if (string.IsNullOrEmpty(data))
                return default;

            return JsonSerializer.Deserialize<T>(data);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var data = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(data))
                return default;

            return JsonSerializer.Deserialize<T>(data);
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration
            };

            var data = JsonSerializer.Serialize(value);
            _cache.SetString(key, data, options);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration
            };

            var data = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, data, options);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
        }

        public async Task RemoveAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }

        public void RemoveByPrefix(string prefix)
        {
            // Redis nao suporta remocao por prefixo nativamente via IDistributedCache
            // Isso requer acesso direto ao Redis com SCAN + DEL
            // Por simplicidade, essa funcao nao e implementada aqui
            throw new NotImplementedException("RemoveByPrefix requer acesso direto ao Redis");
        }

        public bool Exists(string key)
        {
            return _cache.GetString(key) != null;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _cache.GetStringAsync(key) != null;
        }
    }
}
