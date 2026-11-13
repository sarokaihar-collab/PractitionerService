using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using PractitionerService.Api.Fhir;
using PractitionerService.Api.Middleware;
using PractitionerService.Application;
using PractitionerService.Infrastructure;
using PractitionerService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// FHIR: same System.Text.Json pipeline, but with support for the application/fhir+json media type
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    });

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PractitionerDbContext>(name: "database", failureStatus: HealthStatus.Unhealthy);

builder.Services.Configure<MvcOptions>(options =>
{
    foreach (var f in options.InputFormatters.OfType<SystemTextJsonInputFormatter>())
    {
        f.SupportedMediaTypes.Add(FhirConstants.MediaTypeFhirJson);
    }

    foreach (var f in options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>())
    {
        f.SupportedMediaTypes.Add(FhirConstants.MediaTypeFhirJson);
    }
});

// Model binding errors -> OperationOutcome (instead of ProblemDetails)
builder.Services.Configure<ApiBehaviorOptions>(options =>
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Practitioner Service API (FHIR R5)",
        Version = "v1",
        Description =
            "Backend-сервис справочника врачей. " +
            "Реализует FHIR R5-совместимый REST API для ресурса Practitioner. " +
            "Принимает `application/fhir+json` и `application/json`, отдаёт `application/fhir+json`. " +
            "Ошибки возвращаются в формате `OperationOutcome`.",
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    await initializer.InitializeAsync().ConfigureAwait(false);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

app.MapControllers();
app.Run();
