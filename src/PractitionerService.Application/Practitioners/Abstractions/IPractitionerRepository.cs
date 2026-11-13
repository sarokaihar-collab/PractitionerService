using PractitionerService.Application.Common;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Application.Practitioners;

public interface IPractitionerRepository
{
    /// <summary>
    /// Returns the entity regardless of its IsDeleted flag — the caller decides between 404 and 410.
    /// </summary>
    Task<Practitioner?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Finds a practitioner by SourceEventId — used for POST idempotency.</summary>
    Task<Practitioner?> FindBySourceEventIdAsync(string sourceEventId, CancellationToken ct = default);

    /// <summary>Search with filters. Excludes soft-deleted records by default.</summary>
    Task<PagedResult<Practitioner>> SearchAsync(PractitionerFilter filter, CancellationToken ct = default);

    Task AddAsync(Practitioner practitioner, CancellationToken ct = default);
}
