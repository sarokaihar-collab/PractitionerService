using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirBundleEntrySearch
{
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }   // "match" | "include" | "outcome"
}