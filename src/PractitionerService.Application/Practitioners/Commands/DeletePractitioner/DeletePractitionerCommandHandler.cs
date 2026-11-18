using MediatR;
using Microsoft.Extensions.Logging;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Diagnostics;
using PractitionerService.Application.Common.Exceptions;

namespace PractitionerService.Application.Practitioners;

public sealed class DeletePractitionerCommandHandler : IRequestHandler<DeletePractitionerCommand, Unit>
{
    private readonly IPractitionerRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICacheService _cache;
    private readonly IPractitionerMetrics _metrics;
    private readonly ILogger<DeletePractitionerCommandHandler> _logger;

    public DeletePractitionerCommandHandler(IPractitionerRepository repository, IUnitOfWork unitOfWork, IDateTimeProvider clock, ICacheService cache, IPractitionerMetrics metrics, ILogger<DeletePractitionerCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _cache = cache;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeletePractitionerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var p = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (p is null)
        {
            throw new NotFoundException($"Врач {request.Id} не найден.");
        }

        if (p.IsDeleted)
        {
            throw new GoneException($"Врач {request.Id} уже удалён.");
        }

        p.MarkAsDeleted(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateCacheSafeAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

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
