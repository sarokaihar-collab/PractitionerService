using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirQualification
{
    [JsonPropertyName("code")]
    public FhirCodeableConcept? Code { get; set; }
}