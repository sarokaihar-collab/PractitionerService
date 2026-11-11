using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirExtension
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("valueString")]
    public string? ValueString { get; set; }
}