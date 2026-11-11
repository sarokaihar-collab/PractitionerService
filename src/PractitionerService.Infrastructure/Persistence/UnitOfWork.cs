using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace PractitionerService.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private const string UniqueViolation = "23505";

    private readonly PractitionerDbContext _db;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(PractitionerDbContext db, ILogger<UnitOfWork> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Конфликт версий при сохранении данных.");
            throw new ConflictException("Данные были изменены другим запросом. Обновите данные и повторите.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation } pg)
        {
            _logger.LogWarning(ex, "Нарушено уникальное ограничение '{ConstraintName}'.", pg.ConstraintName);
            throw new UniqueConstraintViolationException($"Нарушено уникальное ограничение '{pg.ConstraintName}'.", ex);
        }
    }
}