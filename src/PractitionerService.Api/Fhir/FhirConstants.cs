namespace PractitionerService.Api.Fhir;

public static class FhirConstants
{
    public const string ResourceTypePractitioner = "Practitioner";
    public const string ResourceTypeBundle = "Bundle";
    public const string ResourceTypeOperationOutcome = "OperationOutcome";
    public const string MediaTypeFhirJson = "application/fhir+json";
    public const string PlaceOfWorkExtensionUrl = "http://example.org/fhir/StructureDefinition/place-of-work";
    public const string SourceEventIdExtensionUrl = "http://example.org/fhir/StructureDefinition/source-event-id";
    public const string IssueTypeNotFound = "not-found";
    public const string IssueTypeDeleted = "deleted";
    public const string IssueTypeConflict = "conflict";
    public const string IssueTypeDuplicate = "duplicate";
    public const string IssueTypeBusinessRule = "business-rule";
    public const string IssueTypeStructure = "structure";
    public const string IssueTypeRequired = "required";
    public const string IssueTypeException = "exception";
    public const string SeverityError = "error";
    public const string SeverityWarning = "warning";
}