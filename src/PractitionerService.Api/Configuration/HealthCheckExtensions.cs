using Microsoft.Extensions.Diagnostics.HealthChecks;
using PractitionerService.Infrastructure.Persistence;

namespace PractitionerService.Api.Configuration;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<PractitionerDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy);

        return services;
    }
}