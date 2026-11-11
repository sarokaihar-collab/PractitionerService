using System.Text.Json.Serialization;

public sealed class FhirOperationOutcome
{
    [JsonPropertyName("resourceType")]
    [JsonPropertyOrder(0)]
    public string ResourceType { get; set; } = "OperationOutcome";

    [JsonPropertyName("issue")]
    public List<FhirOperationOutcomeIssue> Issue { get; set; } = new();
}