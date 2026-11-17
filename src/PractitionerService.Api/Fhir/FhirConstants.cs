namespace PractitionerService.Api.Fhir;

public static class FhirConstants
{
    public const string ResourceTypePractitioner = "Practitioner";
    public const string ResourceTypeBundle = "Bundle";
    public const string ResourceTypeOperationOutcome = "OperationOutcome";
    public const string MediaTypeFhirJson = "application/fhir+json";
    public const string MetaTagSystemCreatedAt = "urn:practitioner-service:meta:created-at";
    public const string MetaTagSystemSourceEventId = "urn:practitioner-service:meta:source-event-id";
    public const string ContainedOrganizationId = "place-of-work";
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

    // Telecom systems
    public const string TelecomSystemPhone = "phone";
    public const string TelecomSystemEmail = "email";

    // Bundle types
    public const string BundleTypeSearchset = "searchset";

    // Bundle entry modes
    public const string BundleEntryModeMatch = "match";

    // Link relations
    public const string LinkRelationSelf = "self";
    public const string LinkRelationNext = "next";
    public const string LinkRelationPrevious = "previous";

    // Event types (Redis Streams)
    public const string EventTypeCreated = "created";

    public const string IdempotencyKeyPrefix = "idem:";
    public const int IdempotencyKeyMaxLength = 200;
}
