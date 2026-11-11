using MediatR;

public sealed record SearchPractitionersQuery(PractitionerFilter Filter) : IRequest<PagedResult<PractitionerDto>>;