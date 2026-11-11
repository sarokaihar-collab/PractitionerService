public sealed record PractitionerReadModel(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string Speciality,
    string PlaceOfWork,
    string Email,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version,
    string? SourceEventId);