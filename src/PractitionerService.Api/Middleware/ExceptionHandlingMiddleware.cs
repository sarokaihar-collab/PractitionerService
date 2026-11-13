using PractitionerService.Api.Fhir;
using PractitionerService.Api.Fhir.Models;
using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Domain.Common.Exceptions;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace PractitionerService.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
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
            await _next(context);
        }
        catch (Exception ex) when (MapStatus(ex) is { } status)
        {
            _logger.LogWarning(ex, "Запрос завершился ошибкой: {Message}", ex.Message);

            var outcome = FhirOperationOutcomeFactory.FromException(ex);
            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = FhirConstants.MediaTypeFhirJson;

            var json = JsonSerializer.Serialize(outcome, SerializeOptions);
            await context.Response.WriteAsync(json);
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
                        Diagnostics = "Внутренняя ошибка сервера."
                    }
                }
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(outcome, SerializeOptions));
        }
    }

    private static int? MapStatus(Exception ex) => ExceptionMap.Resolve(ex)?.Status;
}