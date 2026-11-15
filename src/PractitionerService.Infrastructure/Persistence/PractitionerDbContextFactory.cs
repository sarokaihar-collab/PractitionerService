using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PractitionerService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` tooling only.
/// Not used at runtime — the application resolves DbContext through DI
/// with a connection string from configuration (appsettings / env vars).
///
/// Set the <c>ConnectionStrings__Default</c> environment variable to point
/// design-time operations (e.g. <c>dotnet ef database update</c>) at a real
/// database. <c>dotnet ef migrations add</c> does NOT connect to the DB,
/// so the placeholder below is safe for pure migration generation.
/// </summary>
public sealed class PractitionerDbContextFactory : IDesignTimeDbContextFactory<PractitionerDbContext>
{
    // Placeholder — deliberately not a working credential.
    // `dotnet ef migrations add` does not connect; `dotnet ef database update`
    // requires the ConnectionStrings__Default env var to be set.
    private const string DesignTimePlaceholderConnectionString = "Host=localhost;Port=5434;Database=practitioners;Username=postgres;Password=__SET_VIA_ENV__";

    public PractitionerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? DesignTimePlaceholderConnectionString;

        var options = new DbContextOptionsBuilder<PractitionerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PractitionerDbContext(options);
    }
}
