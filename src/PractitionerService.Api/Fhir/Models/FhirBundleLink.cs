using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Ссылка пагинации в Bundle.
/// </summary>
public sealed class FhirBundleLink
{
    /// <summary>
    /// Тип ссылки: self, next, previous, first, last.
    /// </summary>
    [JsonPropertyName("relation")]
    public string? Relation { get; set; }

    /// <summary>
    /// Полный URL для перехода по ссылке.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}