namespace PractitionerService.Application.Common.Diagnostics;

/// <summary>
/// Custom application metrics (business-level counters).
/// </summary>
public interface IPractitionerMetrics
{
    /// <summary>Increments the "practitioner created" counter.</summary>
    void PractitionerCreated();

    void CacheInvalidationSucceeded();

    void CacheInvalidationFailed();

    void EventProcessed(string status);   // "success" | "skipped" | "business-error"

    void EventFailed(string reason);       // "infrastructure" | "exhausted"
}
