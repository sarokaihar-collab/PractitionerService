using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PractitionerService.Application.Common;
using PractitionerService.Application.Practitioners;
using PractitionerService.Infrastructure.Caching;
using PractitionerService.Infrastructure.Messaging;
using PractitionerService.Infrastructure.Options;
using PractitionerService.Infrastructure.Persistence;
using PractitionerService.Infrastructure.Persistence.Repositories;
using PractitionerService.Infrastructure.Time;
using StackExchange.Redis;

namespace PractitionerService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RedisOptions>().Bind(configuration.GetSection(RedisOptions.Section)).ValidateDataAnnotations().ValidateOnStart();

        // === DbContext (uses validated IOptions<DatabaseOptions>) ===
        services.AddDbContext<PractitionerDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(db.Default);
        });

        // === Redis connection multiplexer ===
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redis = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redis.Configuration);
        });

        // === IDistributedCache backed by Redis ===
        services.AddStackExchangeRedisCache(options =>
        {
            // Read directly from configuration: AddStackExchangeRedisCache does not
            // expose IOptions<RedisOptions>. Options validation above guarantees that
            // the values are present and valid, so any bad config fails at startup.
            var redis = configuration.GetSection(RedisOptions.Section).Get<RedisOptions>()
                ?? throw new InvalidOperationException($"Секция конфигурации '{RedisOptions.Section}' не найдена.");

            options.Configuration = redis.Configuration;
            options.InstanceName = redis.InstanceName;
        });

        // === Repositories, UoW, clock, cache, seeder ===
        services.AddScoped<IPractitionerRepository, PractitionerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<PractitionerSeeder>();
        services.AddScoped<IDbInitializer, DbInitializer>();

        // === Background consumer ===
        services.AddHostedService<PractitionerEventConsumer>();

        return services;
    }
}
