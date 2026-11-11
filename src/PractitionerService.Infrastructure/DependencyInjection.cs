using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PractitionerService.Infrastructure.Persistence;
using PractitionerService.Infrastructure.Persistence.Repositories;
using PractitionerService.Infrastructure.Time;

namespace PractitionerService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Строка подключения 'Default' не найдена в конфигурации.");

        services.AddDbContext<PractitionerDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IPractitionerRepository, PractitionerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<PractitionerSeeder>();
        services.AddScoped<IDbInitializer, DbInitializer>();

        return services;
    }
}