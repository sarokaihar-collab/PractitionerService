using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Application.Common;

public interface IIdempotencyStore
{
    Task<Practitioner?> FindBySourceEventIdAsync(string sourceEventId, CancellationToken ct);
}
