using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Exceptions;

namespace PractitionerService.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private const string _uniqueViolation = "23505";

    private static readonly Dictionary<string, (UniqueConstraintField Field, string Message)>
        _constraintMap = new(StringComparer.Ordinal)
        {
            ["ux_practitioners_email_active"] = (UniqueConstraintField.Email, "Врач с таким email уже существует."),
            ["ux_practitioners_phone_active"] = (UniqueConstraintField.Phone, "Врач с таким телефоном уже существует."),
            ["ux_practitioners_source_event_id"] = (UniqueConstraintField.SourceEventId, "Запрос с таким идентификатором события уже обработан."),
        };

    private readonly PractitionerDbContext _db;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(PractitionerDbContext db, ILogger<UnitOfWork> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void Detach<T>(T entity)
        where T : class
        => _db.Entry(entity).State = EntityState.Detached;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Optimistic concurrency conflict while saving changes.");
            throw new ConflictException("Данные были изменены другим запросом. Обновите данные и повторите.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: _uniqueViolation } pg)
        {
            var (field, message) = ResolveUniqueViolation(pg.ConstraintName);

            _logger.LogWarning(ex, "Unique constraint '{ConstraintName}' violated.", pg.ConstraintName);

            throw new UniqueConstraintViolationException(field, message, pg.ConstraintName, ex);
        }
    }

    private static (UniqueConstraintField Field, string Message) ResolveUniqueViolation(string? constraintName) =>
        constraintName is not null && _constraintMap.TryGetValue(constraintName, out var v) ? v : (UniqueConstraintField.Unknown, "Нарушено уникальное ограничение данных.");
}
