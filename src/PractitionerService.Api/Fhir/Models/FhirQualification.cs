using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Квалификация врача в формате FHIR Qualification.
/// </summary>
public sealed class FhirQualification
{
    /// <summary>
    /// Код квалификации. Используется text для передачи специальности свободным текстом.
    /// </summary>
    [JsonPropertyName("code")]
    public FhirCodeableConcept? Code { get; set; }
}