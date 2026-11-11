using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Метаданные FHIR-ресурса.
/// </summary>
public sealed class FhirMeta
{
    /// <summary>
    /// Версия ресурса. Увеличивается при каждом обновлении. Используется для optimistic concurrency через If-Match.
    /// </summary>
    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }

    /// <summary>
    /// Время последнего обновления ресурса в UTC.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime? LastUpdated { get; set; }
}