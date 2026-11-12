using MediatR;

public sealed class SearchPractitionersQueryHandler
    : IRequestHandler<SearchPractitionersQuery, PagedResult<PractitionerReadModel>>
{
    private const int MaxCount = 100;
    private const int DefaultCount = 20;
    private readonly IPractitionerRepository _repository;

    public SearchPractitionersQueryHandler(IPractitionerRepository repository) => _repository = repository;

    public async Task<PagedResult<PractitionerReadModel>> Handle(SearchPractitionersQuery q, CancellationToken ct)
    {
        var f = q.Filter;
        var normalized = f with
        {
            Offset = Math.Max(0, f.Offset),
            Count = f.Count <= 0 ? DefaultCount : Math.Min(f.Count, MaxCount)
        };

        var page = await _repository.SearchAsync(normalized, ct);
        return new PagedResult<PractitionerReadModel>(page.Items.Select(x => x.ToReadModel()).ToList(), page.Total, page.Offset, page.Count);
    }
}