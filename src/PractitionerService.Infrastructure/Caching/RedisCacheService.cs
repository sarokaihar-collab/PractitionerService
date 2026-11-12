using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PractitionerService.Application.Common;

namespace PractitionerService.Infrastructure.Caching;

internal sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDistributedCache _cache;

    public RedisCacheService(IDistributedCache cache) => _cache = cache;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var bytes = await _cache.GetAsync(key, ct);
        if (bytes is null || bytes.Length == 0) return null;
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        return _cache.SetAsync(key, bytes, options, ct);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default) => _cache.RemoveAsync(key, ct);
}