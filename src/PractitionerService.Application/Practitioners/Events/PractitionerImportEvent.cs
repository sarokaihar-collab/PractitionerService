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
