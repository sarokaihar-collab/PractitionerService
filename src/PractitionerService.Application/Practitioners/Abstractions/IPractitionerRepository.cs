using PractitionerService.Application.Common;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Application.Practitioners;

/// <summary>
/// Persistence abstraction for <see cref="Practitioner"/> aggregates.
/// All write operations are expected to be committed via <see cref="IUnitOfWork"/>.
/// </summary>
public interface IPractitionerRepository
{
    /// <summary>
    /// Returns the practitioner by id, or <c>null</c> if not found.
    /// Returns the entity regardless of its <c>IsDeleted</c> flag — the caller
    /// decides whether to respond with 404 (never existed) or 410 (soft-deleted).
    /// </summary>
    Task<Practitioner?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Finds a practitioner by the external source event id.
    /// Used for POST idempotency: if an event with this id has already been processed,
    /// the existing practitioner is returned instead of creating a duplicate.
    /// </summary>
    Task<Practitioner?> FindBySourceEventIdAsync(string sourceEventId, CancellationToken ct = default);

    /// <summary>
    /// Search practitioners by the given filter. Soft-deleted records are excluded
    /// unless <see cref="PractitionerFilter.IncludeDeleted"/> is set to <c>true</c>.
    /// Returns a paginated result with total count.
    /// </summary>
    Task<PagedResult<Practitioner>> SearchAsync(PractitionerFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Stages a new practitioner for insertion. Does not persist — call
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> to commit.
    /// </summary>
    Task AddAsync(Practitioner practitioner, CancellationToken ct = default);
}
