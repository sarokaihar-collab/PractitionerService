using MediatR;
using PractitionerService.Application.Common;

namespace PractitionerService.Application.Practitioners;

public sealed class SearchPractitionersQueryHandler : IRequestHandler<SearchPractitionersQuery, PagedResult<PractitionerReadModel>>
{
    private const int _maxCount = 100;
    private const int _defaultCount = 20;
    private readonly IPractitionerRepository _repository;

    public SearchPractitionersQueryHandler(IPractitionerRepository repository) => _repository = repository;

    public async Task<PagedResult<PractitionerReadModel>> Handle(SearchPractitionersQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = request.Filter;
        var normalized = filter with
        {
            Offset = Math.Max(0, filter.Offset),
            Count = filter.Count <= 0 ? _defaultCount : Math.Min(filter.Count, _maxCount),
        };

        var page = await _repository.SearchAsync(normalized, cancellationToken).ConfigureAwait(false);
        return new PagedResult<PractitionerReadModel>(page.Items.Select(x => x.ToReadModel()).ToList(), page.Total, page.Offset, page.Count);
    }
}
