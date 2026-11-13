namespace PractitionerService.Application.Common;

/// <summary>
/// Simple cache abstraction. Implementations may use Redis, in-memory, etc.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class;

    Task RemoveAsync(string key, CancellationToken ct = default);
}
