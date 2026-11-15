namespace PractitionerService.Application.Common;

/// <summary>
/// Simple cache abstraction. Implementations may use Redis, in-memory, etc.
/// Used for GET-by-id caching; write handlers are responsible for invalidation.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or <c>null</c> if absent / expired.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> with an absolute TTL.
    /// Overwrites any existing entry.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Removes the cache entry for <paramref name="key"/>. No-op if the key does not exist.
    /// Called after successful write operations to prevent stale reads.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);
}