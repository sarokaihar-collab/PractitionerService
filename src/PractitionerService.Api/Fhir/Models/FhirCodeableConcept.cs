using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirCodeableConcept
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}