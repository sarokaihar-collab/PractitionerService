namespace PractitionerService.Application.Common.Diagnostics;

/// <summary>
/// Custom application metrics (business-level counters).
/// </summary>
public interface IPractitionerMetrics
{
    /// <summary>Increments the "practitioner created" counter.</summary>
    void PractitionerCreated();
}
