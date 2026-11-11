using System.Text.Json.Serialization;

public sealed class FhirOperationOutcomeIssue
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "error";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "processing";

    [JsonPropertyName("diagnostics")]
    public string? Diagnostics { get; set; }
}