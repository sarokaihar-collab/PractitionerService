using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using PractitionerService.Api.Fhir;
using PractitionerService.Api.Fhir.Models;

namespace PractitionerService.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions _serializeOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex) when (MapStatus(ex) is { } status)
        {
            _logger.LogWarning(ex, "Запрос завершился ошибкой: {Message}", ex.Message);

            var outcome = FhirOperationOutcomeFactory.FromException(ex);
            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = FhirConstants.MediaTypeFhirJson;

            var json = JsonSerializer.Serialize(outcome, _serializeOptions);
            await context.Response.WriteAsync(json, cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необработанная ошибка сервера.");
            context.Response.Clear();
            context.Response.StatusCode = 500;
            context.Response.ContentType = FhirConstants.MediaTypeFhirJson;

            var outcome = new FhirOperationOutcome
            {
                Issue =
                {
                    new FhirOperationOutcomeIssue
                    {
                        Severity = FhirConstants.SeverityError,
                        Code = FhirConstants.IssueTypeException,
                        Diagnostics = "Внутренняя ошибка сервера.",
                    },
                },
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(outcome, _serializeOptions), cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static int? MapStatus(Exception ex) => ExceptionMap.Resolve(ex)?.Status;
}
