namespace PractitionerService.Api.Http;

public static class ETagExtensions
{
    /// <summary>
    /// Sets the ETag header based on the resource version, formatted as FHIR expects: "{version}".
    /// </summary>
    public static void SetETag(this HttpResponse response, int version)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = $"\"{version}\"";
    }
}
