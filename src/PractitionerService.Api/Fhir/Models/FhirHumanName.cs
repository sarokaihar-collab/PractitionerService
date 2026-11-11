using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirHumanName
{
    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("given")]
    public List<string>? Given { get; set; }
}