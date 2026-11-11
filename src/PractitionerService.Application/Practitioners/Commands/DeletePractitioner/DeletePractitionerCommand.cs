using MediatR;

public sealed record DeletePractitionerCommand(Guid Id) : IRequest<Unit>;