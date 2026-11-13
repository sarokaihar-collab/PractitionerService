using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Имя человека в формате FHIR HumanName.
/// </summary>
public sealed class FhirHumanName
{
    /// <summary>
    /// Фамилия. Обязательна.
    /// </summary>
    [JsonPropertyName("family")]
    public string? Family { get; set; }

    /// <summary>
    /// Имя (и отчество при наличии), список. Используется первый элемент — имя.
    /// </summary>
    [JsonPropertyName("given")]
    public List<string>? Given { get; set; }
}
