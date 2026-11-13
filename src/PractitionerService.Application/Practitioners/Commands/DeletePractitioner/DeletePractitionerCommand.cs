using MediatR;

namespace PractitionerService.Application.Practitioners;

public sealed record DeletePractitionerCommand(Guid Id) : IRequest<Unit>;
