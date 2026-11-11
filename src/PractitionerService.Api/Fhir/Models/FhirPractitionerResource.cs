using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirPractitionerResource
{
    [JsonPropertyName("resourceType")]
    [JsonPropertyOrder(0)]
    public string ResourceType { get; set; } = "Practitioner";

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public string? Id { get; set; }

    [JsonPropertyName("meta")]
    [JsonPropertyOrder(2)]
    public FhirMeta? Meta { get; set; }

    [JsonPropertyName("extension")]
    public List<FhirExtension>? Extension { get; set; }

    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    [JsonPropertyName("name")]
    public List<FhirHumanName>? Name { get; set; }

    [JsonPropertyName("telecom")]
    public List<FhirContactPoint>? Telecom { get; set; }

    [JsonPropertyName("qualification")]
    public List<FhirQualification>? Qualification { get; set; }
}