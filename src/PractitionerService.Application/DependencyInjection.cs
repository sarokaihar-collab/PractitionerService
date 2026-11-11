using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>  cfg.RegisterServicesFromAssemblyContaining(typeof(DependencyInjection)));
        return services;
    }
}