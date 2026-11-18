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

    // Retry / DLQ (new)

    /// <summary>Stream for messages that exhausted all retries.</summary>
    public const string DeadLetterStreamKey = "practitioner-events-dlq";

    /// <summary>Maximum delivery attempts before moving to the dead-letter stream.</summary>
    public const int MaxRetries = 5;

    /// <summary>Redis key prefix for per-message attempt counters.</summary>
    public const string AttemptsKeyPrefix = "event:attempts:";

    /// <summary>How long a message must be pending before another consumer may reclaim it.</summary>
    public static readonly TimeSpan ReclaimMinIdle = TimeSpan.FromMinutes(2);

    /// <summary>TTL for attempt counters (defensive cleanup).</summary>
    public static readonly TimeSpan AttemptsKeyTtl = TimeSpan.FromHours(24);
}
