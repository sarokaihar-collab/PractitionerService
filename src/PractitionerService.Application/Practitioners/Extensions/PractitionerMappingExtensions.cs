using PractitionerService.Domain.Practitioners;

public static class PractitionerMappingExtensions
{
    public static PractitionerDto ToDto(this Practitioner p) => new(
        p.Id, p.FirstName, p.LastName, p.Phone, p.Speciality,
        p.PlaceOfWork, p.Email, p.IsActive,
        p.CreatedAt, p.UpdatedAt, p.Version, p.SourceEventId);
}