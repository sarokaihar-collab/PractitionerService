using PractitionerService.Domain.Practitioners;

public static class PractitionerMappingExtensions
{
    /// <summary>
    /// Projects a domain entity into the read model used by queries.
    /// </summary>
    public static PractitionerReadModel ToReadModel(this Practitioner p) => new(
        p.Id, p.FirstName, p.LastName, p.Phone, p.Speciality,
        p.PlaceOfWork, p.Email, p.IsActive,
        p.CreatedAt, p.UpdatedAt, p.Version, p.SourceEventId);
}