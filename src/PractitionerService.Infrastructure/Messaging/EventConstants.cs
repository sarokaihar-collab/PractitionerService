namespace PractitionerService.Application.Practitioners.Events;

/// <summary>
/// Constants for the Redis Streams event transport used for async import.
/// </summary>
public static class EventConstants
{
    /// <summary>Redis Stream key that carries import events.</summary>
    public const string StreamKey = "practitioner-events";

    /// <summary>Redis Stream consumer group name shared by all instances.</summary>
    public const string GroupName = "practitioner-service";

    /// <summary>Name of the field inside the stream entry that holds the JSON payload.</summary>
    public const string PayloadFieldName = "data";

    /// <summary>Supported event type: create a new practitioner.</summary>
    public const string TypeCreated = "created";
}
