using System.Diagnostics.Metrics;

namespace PractitionerService.Application.Common.Diagnostics;

public sealed class PractitionerMetrics : IPractitionerMetrics
{
    public const string MeterName = "PractitionerService";

    private readonly Counter<long> _practitionersCreated;

    public PractitionerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _practitionersCreated = meter.CreateCounter<long>(
            name: "practitioners.created",
            unit: "practitioners",
            description: "Number of practitioners created");
    }

    public void PractitionerCreated() => _practitionersCreated.Add(1);
}
