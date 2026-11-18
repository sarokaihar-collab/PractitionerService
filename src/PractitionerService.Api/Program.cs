using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using PractitionerService.Api.Configuration;
using PractitionerService.Api.Middleware;
using PractitionerService.Application;
using PractitionerService.Infrastructure;
using PractitionerService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// === Services ===
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiControllers();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddAppHealthChecks();
builder.Services.AddAppOpenTelemetry();

builder.Services.AddHttpLogging(o =>
{
    // Do NOT log RequestQueryString: it may contain PII (name, email, phone).
    o.LoggingFields =
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.ResponseStatusCode |
        HttpLoggingFields.Duration;
});

// === App ===
var app = builder.Build();

// DB init (migrations + seeding)
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    await initializer.InitializeAsync().ConfigureAwait(false);
}

app.UseHttpLogging();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapPrometheusScrapingEndpoint();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();
