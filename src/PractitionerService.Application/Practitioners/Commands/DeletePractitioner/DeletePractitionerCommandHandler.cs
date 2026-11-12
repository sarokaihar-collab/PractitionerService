using MediatR;
using PractitionerService.Application.Common;

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

    public async Task<Unit> Handle(DeletePractitionerCommand cmd, CancellationToken ct)
    {
        var p = await _repositorysitory.GetByIdAsync(cmd.Id, ct);
        if (p is null) throw new NotFoundException($"Врач {cmd.Id} не найден.");
        if (p.IsDeleted) throw new GoneException($"Врач {cmd.Id} уже удалён.");

        p.MarkAsDeleted(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        await _cache.RemoveAsync(CacheKeys.PractitionerById(cmd.Id), ct);

        return Unit.Value;
    }
}