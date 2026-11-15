using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PractitionerService.Infrastructure.Persistence;

public interface IDbInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}

internal sealed class DbInitializer : IDbInitializer
{
    private readonly PractitionerDbContext _db;
    private readonly PractitionerSeeder _seeder;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(PractitionerDbContext db, PractitionerSeeder seeder, ILogger<DbInitializer> logger)
    {
        _db = db;
        _seeder = seeder;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Применяем миграции БД...");
        await _db.Database.MigrateAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Миграции применены.");

        await _seeder.SeedAsync(ct).ConfigureAwait(false);
    }
}
