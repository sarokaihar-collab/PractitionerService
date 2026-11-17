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

// === App ===
var app = builder.Build();

// DB init (migrations + seeding)
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    await initializer.InitializeAsync().ConfigureAwait(false);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapPrometheusScrapingEndpoint();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
