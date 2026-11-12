using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PractitionerService.Application.Common;
using PractitionerService.Infrastructure.Caching;
using PractitionerService.Infrastructure.Messaging;
using PractitionerService.Infrastructure.Persistence;
using PractitionerService.Infrastructure.Persistence.Repositories;
using PractitionerService.Infrastructure.Time;
using StackExchange.Redis;

namespace PractitionerService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Строка подключения 'Default' не найдена в конфигурации.");

        services.AddDbContext<PractitionerDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IPractitionerRepository, PractitionerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<PractitionerSeeder>();
        services.AddScoped<IDbInitializer, DbInitializer>();

        var redisConfig = configuration.GetSection("Redis")["Configuration"] ?? throw new InvalidOperationException("Redis:Configuration не задан.");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConfig;
            options.InstanceName = "practitioner-service:";
        });

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddHostedService<PractitionerEventConsumer>();

        return services;
    }
}