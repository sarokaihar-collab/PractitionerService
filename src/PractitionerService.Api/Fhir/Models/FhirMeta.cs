using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>FHIR resource metadata.</summary>
public sealed class FhirMeta
{
    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTime? LastUpdated { get; set; }

    [JsonPropertyName("tag")]
    public List<FhirCoding>? Tag { get; set; }
}
