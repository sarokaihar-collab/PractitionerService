using MediatR;

public sealed record UpdatePractitionerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string Speciality,
    string PlaceOfWork,
    string Email,
    bool? IsActive,
    int? ExpectedVersion) : IRequest<PractitionerDto>;