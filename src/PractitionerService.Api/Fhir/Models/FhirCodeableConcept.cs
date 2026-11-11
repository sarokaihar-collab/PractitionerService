using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Понятие, кодируемое в FHIR CodeableConcept.
/// </summary>
public sealed class FhirCodeableConcept
{
    /// <summary>
    /// Текстовое описание понятия (например, "Кардиолог").
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}