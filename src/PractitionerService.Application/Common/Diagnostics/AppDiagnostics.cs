using System.Diagnostics.Metrics;

namespace PractitionerService.Application.Common.Diagnostics;

public static class AppDiagnostics
{
    public const string ServiceName = "PractitionerService";
    public static readonly Meter Meter = new(ServiceName);
    public static readonly Counter<long> PractitionersCreated = Meter.CreateCounter<long>(
        name: "practitioners.created",
        unit: "practitioners",
        description: "Number of practitioners created");
}
