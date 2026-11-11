namespace PractitionerService.Domain.Common.Exceptions;

/// <summary>
/// Violation of a domain business invariant.
/// The API layer maps it to OperationOutcome with HTTP 422.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}