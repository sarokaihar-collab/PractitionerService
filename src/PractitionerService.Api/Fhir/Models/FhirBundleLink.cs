using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirBundleLink
{
    [JsonPropertyName("relation")]
    public string? Relation { get; set; }   // "self" | "next" | "previous" | "first" | "last"

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}