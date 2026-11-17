using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>FHIR Qualification — speciality + reference to the issuing organization.</summary>
public sealed class FhirQualification
{
    [JsonPropertyName("code")]
    public FhirCodeableConcept? Code { get; set; }

    [JsonPropertyName("issuer")]
    public FhirReference? Issuer { get; set; }
}
