using Microsoft.EntityFrameworkCore;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Infrastructure.Persistence;

public sealed class PractitionerDbContext : DbContext
{
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();

    public PractitionerDbContext(DbContextOptions<PractitionerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Registers CREATE EXTENSION pg_trgm as part of the model.
        // Must come before ApplyConfigurationsFromAssembly so the extension
        // is available when GIN trigram indexes are declared in configurations.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PractitionerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}