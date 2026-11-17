using System.Text.Json.Serialization;

namespace PractitionerService.Api.Fhir.Models;

/// <summary>FHIR Coding — a reference to a code defined by a system.</summary>
public sealed class FhirCoding
{
    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
