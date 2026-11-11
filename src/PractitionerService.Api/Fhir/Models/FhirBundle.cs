using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// FHIR-ресурс Bundle. Возвращается при поиске.
/// </summary>
public sealed class FhirBundle
{
    /// <summary>
    /// Тип ресурса. Всегда "Bundle".
    /// </summary>
    [JsonPropertyName("resourceType")]
    [JsonPropertyOrder(0)]
    public string ResourceType { get; set; } = "Bundle";

    /// <summary>
    /// Тип Bundle. При поиске всегда "searchset".
    /// </summary>
    [JsonPropertyName("type")]
    [JsonPropertyOrder(1)]
    public string Type { get; set; } = "searchset";

    /// <summary>
    /// Общее количество совпадений по фильтру (без учёта пагинации).
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    /// <summary>
    /// Ссылки пагинации: self, previous (если offset &gt; 0), next (если есть ещё записи).
    /// </summary>
    [JsonPropertyName("link")]
    public List<FhirBundleLink>? Link { get; set; }

    /// <summary>
    /// Найденные ресурсы на текущей странице.
    /// </summary>
    [JsonPropertyName("entry")]
    public List<FhirBundleEntry>? Entry { get; set; }
}
