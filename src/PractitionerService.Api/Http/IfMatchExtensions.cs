using PractitionerService.Api.Fhir;
using PractitionerService.Application.Common.Exceptions;

namespace PractitionerService.Api.Http;

public static class IfMatchExtensions
{
    /// <summary>
    /// Parses the If-Match header according to FHIR semantics:
    ///   - "*"          → returns null (no version check, "update if exists")
    ///   - "\"N\""      → returns N (etag, optional weak prefix W/)
    ///   - absent       → returns null
    /// Throws FhirValidationException for malformed values.
    /// </summary>
    public static int? GetIfMatchVersion(this HttpRequest request)
    {
        var ifMatch = request?.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return null;
        }

        var raw = ifMatch.Replace("W/", string.Empty, StringComparison.Ordinal).Trim().Trim('"', ' ');

        if (string.Equals(raw, "*", StringComparison.Ordinal))
        {
            return null;
        }

        if (int.TryParse(raw, out var version))
        {
            return version;
        }

        throw new FhirValidationException(FhirConstants.IssueTypeStructure, $"Некорректный If-Match: '{ifMatch}'. Ожидается \"<version>\" или \"*\".");
    }
}
