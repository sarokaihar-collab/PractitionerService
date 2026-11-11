using Microsoft.EntityFrameworkCore;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Infrastructure.Persistence;

public sealed class PractitionerDbContext : DbContext
{
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();

    public PractitionerDbContext(DbContextOptions<PractitionerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PractitionerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}