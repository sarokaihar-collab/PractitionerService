using Microsoft.Extensions.Diagnostics.HealthChecks;
using PractitionerService.Infrastructure.Persistence;

namespace PractitionerService.Api.Configuration;

public static class HealthCheckExtensions
{
    private static readonly string[] _tags = new[] { "ready" };

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()

            // Readiness: DB + Redis
            .AddDbContextCheck<PractitionerDbContext>(name: "database", failureStatus: HealthStatus.Unhealthy, tags: _tags)
            .AddCheck<RedisHealthCheck>(name: "redis", failureStatus: HealthStatus.Unhealthy, tags: _tags);

        return services;
    }
}
