using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirContactPoint
{
    [JsonPropertyName("system")]
    public string? System { get; set; }   // "phone" | "email"

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}