using MediatR;

namespace PractitionerService.Application.Practitioners;

public sealed record GetPractitionerByIdQuery(Guid Id) : IRequest<PractitionerReadModel>;
