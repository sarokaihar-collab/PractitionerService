using MediatR;
using Microsoft.Extensions.Logging;
using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Domain.Practitioners;

public sealed class CreatePractitionerCommandHandler : IRequestHandler<CreatePractitionerCommand, PractitionerCommandResult>
{
    private readonly IPractitionerRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CreatePractitionerCommandHandler> _logger;

    public CreatePractitionerCommandHandler(IPractitionerRepository repository, IUnitOfWork unitOfWork, IDateTimeProvider clock, ILogger<CreatePractitionerCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PractitionerCommandResult> Handle(CreatePractitionerCommand cmd, CancellationToken ct)
    {
        // Idempotency: if SourceEventId (Idempotency-Key) has already been processed, return the existing entity.
        if (!string.IsNullOrWhiteSpace(cmd.SourceEventId))
        {
            var existing = await _repository.FindBySourceEventIdAsync(cmd.SourceEventId, ct);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotent replay for SourceEventId={SourceEventId}, returning practitioner {Id}", cmd.SourceEventId, existing.Id);
                return new PractitionerCommandResult(existing.Id, existing.Version);
            }
        }

        var practitioner = Practitioner.Create(cmd.FirstName, cmd.LastName, cmd.Phone, cmd.Speciality, cmd.PlaceOfWork, cmd.Email, cmd.IsActive ?? true, _clock.UtcNow, cmd.SourceEventId);

        await _repository.AddAsync(practitioner, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
            when (ex.Field == UniqueConstraintField.SourceEventId && !string.IsNullOrWhiteSpace(cmd.SourceEventId))
        {
            // Race: another request managed to create the same SourceEventId first. Return its result.
            var existing = await _repository.FindBySourceEventIdAsync(cmd.SourceEventId!, ct);
            if (existing is not null) return new PractitionerCommandResult(existing.Id, existing.Version);
            throw;
        }

        return new PractitionerCommandResult(practitioner.Id, practitioner.Version);
    }
}