using MediatR;

public sealed class SearchPractitionersQueryHandler
    : IRequestHandler<SearchPractitionersQuery, PagedResult<PractitionerDto>>
{
    private const int MaxCount = 100;
    private const int DefaultCount = 20;
    private readonly IPractitionerRepository _repo;

    public SearchPractitionersQueryHandler(IPractitionerRepository repo) => _repo = repo;

    public async Task<PagedResult<PractitionerDto>> Handle(SearchPractitionersQuery q, CancellationToken ct)
    {
        var f = q.Filter;
        var normalized = f with
        {
            Offset = Math.Max(0, f.Offset),
            Count = f.Count <= 0 ? DefaultCount : Math.Min(f.Count, MaxCount)
        };

        var page = await _repo.SearchAsync(normalized, ct);
        return new PagedResult<PractitionerDto>(page.Items.Select(x => x.ToDto()).ToList(), page.Total, page.Offset, page.Count);
    }
}