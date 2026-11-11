using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Расширение FHIR-ресурса. Используется для полей, которых нет в стандартной модели Practitioner.
/// </summary>
public sealed class FhirExtension
{
    /// <summary>
    /// URL расширения, определяющий его семантику.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Строковое значение расширения.
    /// </summary>
    [JsonPropertyName("valueString")]
    public string? ValueString { get; set; }
}