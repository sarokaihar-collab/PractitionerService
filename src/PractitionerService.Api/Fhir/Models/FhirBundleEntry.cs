using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirBundleEntry
{
    [JsonPropertyName("fullUrl")]
    public string? FullUrl { get; set; }

    [JsonPropertyName("resource")]
    public FhirPractitionerResource? Resource { get; set; }

    [JsonPropertyName("search")]
    public FhirBundleEntrySearch? Search { get; set; }
}