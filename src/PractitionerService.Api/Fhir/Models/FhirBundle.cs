using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

public sealed class FhirBundle
{
    [JsonPropertyName("resourceType")]
    [JsonPropertyOrder(0)]
    public string ResourceType { get; set; } = "Bundle";

    [JsonPropertyName("type")]
    [JsonPropertyOrder(1)]
    public string Type { get; set; } = "searchset";

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("link")]
    public List<FhirBundleLink>? Link { get; set; }

    [JsonPropertyName("entry")]
    public List<FhirBundleEntry>? Entry { get; set; }
}