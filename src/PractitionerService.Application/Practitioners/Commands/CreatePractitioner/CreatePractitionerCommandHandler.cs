using MediatR;
using Microsoft.Extensions.Logging;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Application.Practitioners;

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

    public async Task<PractitionerCommandResult> Handle(CreatePractitionerCommand request, CancellationToken cancellationToken)
    {
        // Idempotency: if SourceEventId (Idempotency-Key) has already been processed, return the existing entity.
        if (!string.IsNullOrWhiteSpace(request.SourceEventId))
        {
            var existing = await _repository.FindBySourceEventIdAsync(request.SourceEventId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotent replay for SourceEventId={SourceEventId}, returning practitioner {Id}", request.SourceEventId, existing.Id);
                return new PractitionerCommandResult(existing.Id, existing.Version);
            }
        }

        var practitioner = Practitioner.Create(request.FirstName, request.LastName, request.Phone, request.Speciality, request.PlaceOfWork, request.Email, request.IsActive ?? true, _clock.UtcNow, request.SourceEventId);

        await _repository.AddAsync(practitioner, cancellationToken).ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException ex) when (ex.Field == UniqueConstraintField.SourceEventId)
        {
            // Detach the failed attempt so it does not retry on any subsequent SaveChanges
            _unitOfWork.Detach(practitioner);

            var existing = await _repository.FindBySourceEventIdAsync(request.SourceEventId!, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return new PractitionerCommandResult(existing.Id, existing.Version);  // или return (Id, Version)
            }

            throw;
        }

        return new PractitionerCommandResult(practitioner.Id, practitioner.Version);
    }
}
