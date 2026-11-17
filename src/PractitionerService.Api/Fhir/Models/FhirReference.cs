using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>FHIR Reference — a link to another resource (may be contained).</summary>
public sealed class FhirReference
{
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }
}
