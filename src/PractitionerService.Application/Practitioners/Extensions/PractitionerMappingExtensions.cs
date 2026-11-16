using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Application.Practitioners;

public static class PractitionerMappingExtensions
{
    /// <summary>
    /// Projects a domain entity into the read model used by queries.
    /// </summary>
    public static PractitionerReadModel ToReadModel(this Practitioner practitioner)
    {
        ArgumentNullException.ThrowIfNull(practitioner);

        return new PractitionerReadModel(practitioner.Id, practitioner.FirstName, practitioner.LastName, practitioner.Phone, practitioner.Speciality, practitioner.PlaceOfWork, practitioner.Email, practitioner.IsActive, practitioner.CreatedAt, practitioner.UpdatedAt, practitioner.Version, practitioner.SourceEventId);
    }
}
