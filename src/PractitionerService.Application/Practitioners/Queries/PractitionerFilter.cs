namespace PractitionerService.Application.Practitioners;

public sealed record PractitionerFilter(
    string? NameContains = null,
    string? FamilyContains = null,
    string? GivenContains = null,
    string? SpecialityContains = null,
    string? PlaceOfWorkContains = null,
    string? EmailEquals = null,
    string? PhoneContains = null,
    bool? Active = null,
    int Offset = 0,
    int Count = 20,
    bool IncludeDeleted = false);
