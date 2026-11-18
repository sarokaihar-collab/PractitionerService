using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace PractitionerService.Api.Configuration;

/// <summary>
/// Health check that pings Redis. Reports Unhealthy if Redis is unreachable.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.PingAsync().ConfigureAwait(false);
            return HealthCheckResult.Healthy("Redis доступен.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis недоступен.", ex);
        }
    }
}
