using MediatR;

public sealed record GetPractitionerByIdQuery(Guid Id) : IRequest<PractitionerDto>;