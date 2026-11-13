namespace PractitionerService.Application.Practitioners.Events;

/// <summary>
/// Import event for practitioner data coming from an external system via the message bus.
/// </summary>
public sealed class PractitionerImportEvent
{
    public string EventId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public PractitionerImportPayload? Payload { get; set; }
}

public sealed class PractitionerImportPayload
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Speciality { get; set; } = string.Empty;
    public string PlaceOfWork { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool? IsActive { get; set; }
}
