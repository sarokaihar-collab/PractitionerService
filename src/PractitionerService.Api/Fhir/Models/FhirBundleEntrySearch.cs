using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Метаданные поиска для записи Bundle.
/// </summary>
public sealed class FhirBundleEntrySearch
{
    /// <summary>
    /// Режим: "match" — совпадение по фильтру.
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}
