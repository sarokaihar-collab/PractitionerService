using PractitionerService.Domain.Practitioners;

public interface IIdempotencyStore
{
    Task<Practitioner?> FindBySourceEventIdAsync(string sourceEventId, CancellationToken ct);
}