using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace PractitionerService.Api.Configuration;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddAppOpenTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation() // Metrics for incoming HTTP requests
                    .AddHttpClientInstrumentation() // Metrics for outgoing HTTP requests
                    .AddRuntimeInstrumentation() // .NET Runtime metrics (GC, CPU, ThreadPool)
                    .AddMeter("PractitionerService")
                    .AddPrometheusExporter();  // Export in Prometheus format
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation() // Traces for incoming requests
                    .AddHttpClientInstrumentation();   // Traces for outgoing requests
            });

        return services;
    }
}
