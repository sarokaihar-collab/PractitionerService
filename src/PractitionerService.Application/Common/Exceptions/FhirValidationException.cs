namespace PractitionerService.Application.Common.Exceptions;

/// <summary>
/// Violation of the FHIR resource structure (required fields, format).
/// The API layer maps it to OperationOutcome with HTTP 422.
/// </summary>
public sealed class FhirValidationException : Exception
{
    public FhirValidationException(string fhirIssueCode, string message)
        : base(message)
    {
        FhirIssueCode = fhirIssueCode;
    }

    public string FhirIssueCode { get; }
}
