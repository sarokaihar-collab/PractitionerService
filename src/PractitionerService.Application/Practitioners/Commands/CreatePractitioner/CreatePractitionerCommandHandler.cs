using MediatR;
using Microsoft.Extensions.Logging;
using PractitionerService.Domain.Practitioners;

public sealed class CreatePractitionerCommandHandler : IRequestHandler<CreatePractitionerCommand, PractitionerDto>
{
    private readonly IPractitionerRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CreatePractitionerCommandHandler> _logger;

    public CreatePractitionerCommandHandler(IPractitionerRepository repo, IUnitOfWork uow, IDateTimeProvider clock, ILogger<CreatePractitionerCommandHandler> logger)
    {
        _repo = repo;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PractitionerDto> Handle(CreatePractitionerCommand cmd, CancellationToken ct)
    {
        // Idempotency: if SourceEventId (Idempotency-Key) has already been processed, return the existing entity.
        if (!string.IsNullOrWhiteSpace(cmd.SourceEventId))
        {
            var existing = await _repo.FindBySourceEventIdAsync(cmd.SourceEventId, ct);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotent replay for SourceEventId={SourceEventId}, returning practitioner {Id}", cmd.SourceEventId, existing.Id);
                return existing.ToDto();
            }
        }

        var practitioner = Practitioner.Create(cmd.FirstName, cmd.LastName, cmd.Phone, cmd.Speciality, cmd.PlaceOfWork, cmd.Email, cmd.IsActive ?? true, _clock.UtcNow, cmd.SourceEventId);

        await _repo.AddAsync(practitioner, ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException) when (!string.IsNullOrWhiteSpace(cmd.SourceEventId))
        {
            // Race: another request managed to create the same SourceEventId first. Return its result.
            var existing = await _repo.FindBySourceEventIdAsync(cmd.SourceEventId!, ct);
            if (existing is not null) return existing.ToDto();
            throw;
        }

        return practitioner.ToDto();
    }
}