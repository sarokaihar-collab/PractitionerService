using PractitionerService.Api.Fhir.Models;

namespace PractitionerService.Api.Fhir;

public static class FhirOperationOutcomeFactory
{
    public static FhirOperationOutcome FromException(Exception ex)
    {
        var code = ExceptionMap.ResolveFhirCode(ex);
        var diagnostics = ExceptionMap.Resolve(ex) is not null ? ex?.Message : "Внутренняя ошибка сервера.";

        return new FhirOperationOutcome
        {
            Issue =
            {
                new FhirOperationOutcomeIssue
                {
                    Severity = FhirConstants.SeverityError,
                    Code = code,
                    Diagnostics = diagnostics,
                },
            },
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
                    Diagnostics = $"Параметр '{field}': {message}",
                });
            }
        }

        return outcome;
    }
}
