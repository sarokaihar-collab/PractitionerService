using System.Diagnostics.Metrics;

namespace PractitionerService.Application.Common.Diagnostics;

public sealed class PractitionerMetrics : IPractitionerMetrics
{
    public const string MeterName = "PractitionerService";

    private readonly Counter<long> _practitionersCreated;
    private readonly Counter<long> _cacheInvalidations;
    private readonly Counter<long> _eventsProcessed;
    private readonly Counter<long> _eventsFailed;

    public PractitionerMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);

        _practitionersCreated = meter.CreateCounter<long>("practitioners.created", "practitioners", "Number of practitioners created");

        _cacheInvalidations = meter.CreateCounter<long>("cache.invalidations", "operations", "Cache invalidation attempts by status");

        _eventsProcessed = meter.CreateCounter<long>("events.processed", "events", "Events processed by the consumer, by status");

        _eventsFailed = meter.CreateCounter<long>("events.failed", "events", "Events failed in the consumer, by reason");
    }

    public void PractitionerCreated() => _practitionersCreated.Add(1);

    public void CacheInvalidationSucceeded() => _cacheInvalidations.Add(1, new KeyValuePair<string, object?>("status", "success"));

    public void CacheInvalidationFailed() => _cacheInvalidations.Add(1, new KeyValuePair<string, object?>("status", "failure"));

    public void EventProcessed(string status) => _eventsProcessed.Add(1, new KeyValuePair<string, object?>("status", status));

    public void EventFailed(string reason) => _eventsFailed.Add(1, new KeyValuePair<string, object?>("reason", reason));
}
