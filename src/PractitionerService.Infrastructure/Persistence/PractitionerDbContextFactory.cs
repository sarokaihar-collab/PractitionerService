using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PractitionerService.Infrastructure.Persistence;

/// <summary>
/// Used only by the `dotnet ef` tooling for migration generation.
/// At runtime the DbContext is registered through DependencyInjection with the real connection string.
/// </summary>
public sealed class PractitionerDbContextFactory : IDesignTimeDbContextFactory<PractitionerDbContext>
{
    public PractitionerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PractitionerDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=practitioners;Username=postgres;Password=postgres")
            .Options;
        return new PractitionerDbContext(options);
    }
}