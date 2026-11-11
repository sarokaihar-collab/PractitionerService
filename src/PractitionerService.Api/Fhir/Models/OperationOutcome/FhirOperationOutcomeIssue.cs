using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>
/// Одна проблема в OperationOutcome.
/// </summary>
public sealed class FhirOperationOutcomeIssue
{
    /// <summary>
    /// Серьёзность: "error" или "warning".
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "error";

    /// <summary>
    /// Машинно-читаемый код проблемы: not-found, deleted, conflict, duplicate, required, structure, business-rule, exception.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = "processing";

    /// <summary>
    /// Человекочитаемое описание проблемы (на русском).
    /// </summary>
    [JsonPropertyName("diagnostics")]
    public string? Diagnostics { get; set; }
}