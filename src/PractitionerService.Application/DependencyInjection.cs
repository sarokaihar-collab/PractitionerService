using Microsoft.Extensions.DependencyInjection;
using PractitionerService.Application.Common.Diagnostics;

namespace PractitionerService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IPractitionerMetrics, PractitionerMetrics>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(DependencyInjection)));
        return services;
    }
}
