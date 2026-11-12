using MediatR;
using PractitionerService.Application.Common;

namespace PractitionerService.Application.Practitioners.Queries;

public sealed class GetPractitionerByIdQueryHandler : IRequestHandler<GetPractitionerByIdQuery, PractitionerReadModel>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IPractitionerRepository _repo;
    private readonly ICacheService _cache;

    public GetPractitionerByIdQueryHandler(IPractitionerRepository repo, ICacheService cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<PractitionerReadModel> Handle(GetPractitionerByIdQuery q, CancellationToken ct)
    {
        var cacheKey = CacheKeys.PractitionerById(q.Id);

        var cached = await _cache.GetAsync<PractitionerReadModel>(cacheKey, ct);
        if (cached is not null) 
            return cached;

        var p = await _repo.GetByIdAsync(q.Id, ct);
        if (p is null) throw new NotFoundException($"Practitioner {q.Id} не найден.");
        if (p.IsDeleted) throw new GoneException($"Practitioner {q.Id} удалён.");

        var model = p.ToReadModel();

        await _cache.SetAsync(cacheKey, model, CacheTtl, ct);
        return model;
    }
}