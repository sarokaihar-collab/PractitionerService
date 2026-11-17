using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>FHIR Organization — used as a contained resource for place-of-work.</summary>
public sealed class FhirOrganization
{
    [JsonPropertyName("resourceType")]
    [JsonPropertyOrder(0)]
    public string ResourceType { get; set; } = "Organization";

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
