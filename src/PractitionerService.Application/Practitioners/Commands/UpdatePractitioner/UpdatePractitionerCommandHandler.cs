using MediatR;
using Microsoft.Extensions.Logging;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Diagnostics;
using PractitionerService.Application.Common.Exceptions;

namespace PractitionerService.Application.Practitioners;

public sealed class UpdatePractitionerCommandHandler : IRequestHandler<UpdatePractitionerCommand, PractitionerCommandResult>
{
    private readonly IPractitionerRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICacheService _cache;
    private readonly IPractitionerMetrics _metrics;
    private readonly ILogger<UpdatePractitionerCommandHandler> _logger;

    public UpdatePractitionerCommandHandler(IPractitionerRepository repository, IUnitOfWork unitOfWork, IDateTimeProvider clock, ICacheService cache, IPractitionerMetrics metrics, ILogger<UpdatePractitionerCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _cache = cache;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<PractitionerCommandResult> Handle(UpdatePractitionerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var p = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (p is null)
        {
            throw new NotFoundException($"Practitioner {request.Id} не найден.");
        }

        if (p.IsDeleted)
        {
            throw new GoneException($"Practitioner {request.Id} удалён и не может быть изменён.");
        }

        if (request.ExpectedVersion.HasValue && p.Version != request.ExpectedVersion.Value)
        {
            throw new ConflictException($"Версия Practitioner {request.Id} изменилась (ожидалась {request.ExpectedVersion}, текущая {p.Version}).");
        }

        p.Update(request.FirstName, request.LastName, request.Phone, request.Speciality, request.PlaceOfWork, request.Email, request.IsActive ?? p.IsActive, _clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateCacheSafeAsync(request.Id, cancellationToken).ConfigureAwait(false);

        return new PractitionerCommandResult(p.Id, p.Version);
    }

    /// <summary>
    /// Removes the cached entry, tolerating Redis outages. On failure, logs + increments
    /// a metric — the entry will expire naturally via TTL (5 min).
    /// </summary>
    private async Task InvalidateCacheSafeAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _cache.RemoveAsync(CacheKeys.PractitionerById(id), ct).ConfigureAwait(false);
            _metrics.CacheInvalidationSucceeded();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось инвалидировать кэш для practitioner {Id}. Запись истечёт по TTL.", id);
            _metrics.CacheInvalidationFailed();
        }
    }
}
