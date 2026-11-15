using MediatR;
using PractitionerService.Application.Common;

namespace PractitionerService.Application.Practitioners;

public sealed record SearchPractitionersQuery(PractitionerFilter Filter) : IRequest<PagedResult<PractitionerReadModel>>;
