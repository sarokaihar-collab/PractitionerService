using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Контактная точка (телефон или email) в формате FHIR ContactPoint.
/// </summary>
public sealed class FhirContactPoint
{
    /// <summary>
    /// Тип контакта. Допустимые значения: "phone" (телефон), "email" (электронная почта).
    /// </summary>
    [JsonPropertyName("system")]
    public string? System { get; set; }

    /// <summary>
    /// Значение контакта: номер телефона или адрес электронной почты.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
