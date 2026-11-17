using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.OpenApi;
using PractitionerService.Api.Fhir;

namespace PractitionerService.Api.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers controllers with FHIR-aware JSON options and the
    /// custom model-binding error formatter (OperationOutcome instead of ProblemDetails).
    /// </summary>
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            });

        services.Configure<MvcOptions>(options =>
        {
            foreach (var formatter in options.InputFormatters.OfType<SystemTextJsonInputFormatter>())
            {
                formatter.SupportedMediaTypes.Add(FhirConstants.MediaTypeFhirJson);
            }

            foreach (var formatter in options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>())
            {
                formatter.SupportedMediaTypes.Add(FhirConstants.MediaTypeFhirJson);
            }
        });

        // Model-binding errors → OperationOutcome
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal);

                var outcome = FhirOperationOutcomeFactory.FromModelState(errors);
                return new BadRequestObjectResult(outcome)
                {
                    ContentTypes = { FhirConstants.MediaTypeFhirJson },
                };
            };
        });

        return services;
    }

    /// <summary>Registers Swagger with XML comments from the Api assembly.</summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.EnableAnnotations();

            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Practitioner Service API (FHIR R5)",
                Version = "v1",
                Description =
                    "Backend-сервис справочника врачей. " +
                    "Реализует FHIR R5-совместимый REST API для ресурса Practitioner. " +
                    "Принимает `application/fhir+json` и `application/json`, " +
                    "отдаёт `application/fhir+json`. " +
                    "Ошибки возвращаются в формате `OperationOutcome`.",
            });

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });

        return services;
    }
}
