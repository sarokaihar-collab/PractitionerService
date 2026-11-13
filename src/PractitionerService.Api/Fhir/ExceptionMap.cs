using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Domain.Common.Exceptions;

namespace PractitionerService.Api.Fhir;

/// <summary>
/// Single source of truth mapping application/domain exceptions
/// to HTTP status codes and FHIR issue codes.
/// </summary>
public static class ExceptionMap
{
    private static readonly Dictionary<Type, (int Status, string FhirCode)> Map = new()
    {
        [typeof(NotFoundException)] = (404, FhirConstants.IssueTypeNotFound),
        [typeof(GoneException)] = (410, FhirConstants.IssueTypeDeleted),
        [typeof(ConflictException)] = (409, FhirConstants.IssueTypeConflict),
        [typeof(UniqueConstraintViolationException)] = (409, FhirConstants.IssueTypeDuplicate),
        [typeof(FhirValidationException)] = (422, FhirConstants.IssueTypeStructure),
        [typeof(DomainException)] = (422, FhirConstants.IssueTypeBusinessRule),
    };

    /// <summary>
    /// Returns HTTP status and FHIR code for the given exception, or null if unknown.
    /// </summary>
    public static (int Status, string FhirCode)? Resolve(Exception ex)
        => Map.TryGetValue(ex.GetType(), out var v) ? v : null;

    /// <summary>
    /// Resolves the FHIR issue code. For <see cref="FhirValidationException"/>,
    /// the exception's own <see cref="FhirValidationException.FhirIssueCode"/> wins.
    /// </summary>
    public static string ResolveFhirCode(Exception ex) =>
        ex is FhirValidationException fhir && !string.IsNullOrEmpty(fhir.FhirIssueCode)
            ? fhir.FhirIssueCode
            : Resolve(ex)?.FhirCode ?? FhirConstants.IssueTypeException;
}