using MediatR;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Exceptions;

namespace PractitionerService.Application.Practitioners;

public sealed class DeletePractitionerCommandHandler : IRequestHandler<DeletePractitionerCommand, Unit>
{
    private readonly IPractitionerRepository _repositorysitory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICacheService _cache;

    public DeletePractitionerCommandHandler(IPractitionerRepository repositorysitory, IUnitOfWork unitOfWork, IDateTimeProvider clock, ICacheService cache)
    {
        _repositorysitory = repositorysitory;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _cache = cache;
    }

    public async Task<Unit> Handle(DeletePractitionerCommand request, CancellationToken cancellationToken)
    {
        var p = await _repositorysitory.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
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

        await _cache.RemoveAsync(CacheKeys.PractitionerById(request.Id), cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
