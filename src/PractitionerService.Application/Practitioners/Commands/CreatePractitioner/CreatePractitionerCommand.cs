using MediatR;

public sealed record CreatePractitionerCommand(
    string FirstName,
    string LastName,
    string Phone,
    string Speciality,
    string PlaceOfWork,
    string Email,
    bool? IsActive,
    string? SourceEventId) : IRequest<PractitionerCommandResult>;