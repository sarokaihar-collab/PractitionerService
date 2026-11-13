using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Запись Bundle — один найденный ресурс.
/// </summary>
public sealed class FhirBundleEntry
{
    /// <summary>
    /// Канонический URL ресурса.
    /// </summary>
    [JsonPropertyName("fullUrl")]
    public string? FullUrl { get; set; }

    /// <summary>
    /// Сам ресурс.
    /// </summary>
    [JsonPropertyName("resource")]
    public FhirPractitionerResource? Resource { get; set; }

    /// <summary>
    /// Метаданные поиска. mode = "match" для найденных ресурсов.
    /// </summary>
    [JsonPropertyName("search")]
    public FhirBundleEntrySearch? Search { get; set; }
}
