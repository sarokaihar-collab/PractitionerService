using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// FHIR R5-ресурс Practitioner. Представляет врача в справочнике.
/// </summary>
public sealed class FhirPractitionerResource
{
    /// <summary>
    /// Тип ресурса. Всегда "Practitioner". Обязателен.
    /// </summary>
    [JsonPropertyName("resourceType")]
    [JsonPropertyOrder(0)]
    public string ResourceType { get; set; } = "Practitioner";

    /// <summary>
    /// Логический идентификатор ресурса. Генерируется сервером, при создании не передаётся.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public string? Id { get; set; }

    /// <summary>
    /// Метаданные ресурса: версия (versionId) и время последнего обновления (lastUpdated).
    /// Заполняются сервером.
    /// </summary>
    [JsonPropertyName("meta")]
    [JsonPropertyOrder(2)]
    public FhirMeta? Meta { get; set; }

    /// <summary>
    /// Дополнительные поля. Используются для передачи места работы
    /// (http://example.org/fhir/StructureDefinition/place-of-work) и
    /// идентификатора события (http://example.org/fhir/StructureDefinition/source-event-id).
    /// </summary>
    [JsonPropertyName("extension")]
    public List<FhirExtension>? Extension { get; set; }

    /// <summary>
    /// Признак активности врача (true — активен, false — неактивен). Опционально, при создании по умолчанию true.
    /// </summary>
    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    /// <summary>
    /// ФИО врача. Обязательно, должен быть хотя бы один элемент. Используется первый: family — фамилия, given[0] — имя.
    /// </summary>
    [JsonPropertyName("name")]
    public List<FhirHumanName>? Name { get; set; }

    /// <summary>
    /// Контактные данные. Обязательно, должны присутствовать phone и email.
    /// </summary>
    [JsonPropertyName("telecom")]
    public List<FhirContactPoint>? Telecom { get; set; }

    /// <summary>
    /// Квалификации врача. Используется первый элемент: code.text — специальность.
    /// </summary>
    [JsonPropertyName("qualification")]
    public List<FhirQualification>? Qualification { get; set; }
}