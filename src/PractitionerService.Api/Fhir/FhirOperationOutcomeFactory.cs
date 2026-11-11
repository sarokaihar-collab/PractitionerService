using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Domain.Common.Exceptions;

namespace PractitionerService.Api.Fhir;

public static class FhirOperationOutcomeFactory
{
    public static FhirOperationOutcome FromException(Exception ex)
    {
        var (code, diagnostics) = ex switch
        {
            NotFoundException nf => (FhirConstants.IssueTypeNotFound, nf.Message),
            GoneException g => (FhirConstants.IssueTypeDeleted, g.Message),
            ConflictException c => (FhirConstants.IssueTypeConflict, c.Message),
            UniqueConstraintViolationException u => (FhirConstants.IssueTypeDuplicate, u.Message),
            FhirValidationException f => (f.FhirIssueCode, f.Message),
            DomainException d => (FhirConstants.IssueTypeBusinessRule, d.Message),
            _ => (FhirConstants.IssueTypeException, "Внутренняя ошибка сервера.")
        };

        return new FhirOperationOutcome
        {
            Issue =
            {
                new FhirOperationOutcomeIssue
                {
                    Severity = FhirConstants.SeverityError,
                    Code = code,
                    Diagnostics = diagnostics
                }
            }
        };
    }

    public static FhirOperationOutcome FromModelState(IDictionary<string, string[]> errors)
    {
        var outcome = new FhirOperationOutcome();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                outcome.Issue.Add(new FhirOperationOutcomeIssue
                {
                    Severity = FhirConstants.SeverityError,
                    Code = FhirConstants.IssueTypeStructure,
                    Diagnostics = $"Параметр '{field}': {message}"
                });
            }
        }
        return outcome;
    }
}