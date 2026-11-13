using System.Text.Json.Serialization;
using PractitionerService.Api.Fhir.Models;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// FHIR-ресурс OperationOutcome. Возвращается при ошибках вместо ProblemDetails.
/// </summary>
public sealed class FhirOperationOutcome
{
    /// <summary>
    /// Тип ресурса. Всегда "OperationOutcome".
    /// </summary>
    [JsonPropertyName("resourceType")]
    [JsonPropertyOrder(0)]
    public string ResourceType { get; set; } = "OperationOutcome";

    /// <summary>
    /// Список проблем. Обычно содержит один элемент.
    /// </summary>
    [JsonPropertyName("issue")]
    public List<FhirOperationOutcomeIssue> Issue { get; set; } = new();
}
