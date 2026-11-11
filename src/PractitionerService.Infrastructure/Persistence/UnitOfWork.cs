using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using PractitionerService.Application.Common.Exceptions;

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
            _logger.LogWarning(ex, "Optimistic concurrency conflict while saving changes.");
            throw new ConflictException("Данные были изменены другим запросом. Обновите данные и повторите.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation } pg)
        {
            var (field, message) = ResolveUniqueViolation(pg.ConstraintName);

            _logger.LogWarning(ex, "Unique constraint '{ConstraintName}' violated.", pg.ConstraintName);

            throw new UniqueConstraintViolationException(field, message, pg.ConstraintName, ex);
        }
    }

    /// <summary>
    /// Maps a PostgreSQL unique index name to a strongly-typed field and a user-friendly message.
    /// Keeps DB-specific identifiers out of the Application layer.
    /// </summary>
    private static (UniqueConstraintField Field, string Message) ResolveUniqueViolation(string? constraintName) =>
        constraintName switch
        {
            "ux_practitioners_email_active" => (UniqueConstraintField.Email, "Врач с таким email уже существует."),

            "ux_practitioners_phone_active" => (UniqueConstraintField.Phone, "Врач с таким телефоном уже существует."),

            "ux_practitioners_source_event_id" => (UniqueConstraintField.SourceEventId, "Запрос с таким идентификатором события уже обработан."),

            _ => (UniqueConstraintField.Unknown, "Нарушено уникальное ограничение данных.")
        };
}