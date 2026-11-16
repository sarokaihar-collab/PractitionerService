using MediatR;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Exceptions;

namespace PractitionerService.Application.Practitioners.Queries;

public sealed class GetPractitionerByIdQueryHandler : IRequestHandler<GetPractitionerByIdQuery, PractitionerReadModel>
{
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    private readonly IPractitionerRepository _repository;
    private readonly ICacheService _cache;

    public GetPractitionerByIdQueryHandler(IPractitionerRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<PractitionerReadModel> Handle(GetPractitionerByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cacheKey = CacheKeys.PractitionerById(request.Id);

        var cached = await _cache.GetAsync<PractitionerReadModel>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var p = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (p is null)
        {
            throw new NotFoundException($"Врач {request.Id} не найден.");
        }

        if (p.IsDeleted)
        {
            throw new GoneException($"Врач {request.Id} удалён.");
        }

        var model = p.ToReadModel();

        await _cache.SetAsync(cacheKey, model, _cacheTtl, cancellationToken).ConfigureAwait(false);
        return model;
    }
}
