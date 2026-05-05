using RetailPos.Sales.Worker;
using Serilog;

// ASP.NET Core Web application — NOT a console app.
// BackgroundService is hosted within the ASP.NET Core host,
// so it shares health checks, configuration, and DI with the main API.
// This can be deployed as a separate Deployment in Kubernetes
// or co-hosted in the same pod as the Sales API.

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.OpenTelemetry().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddRuntimeInstrumentation())
    .WithTracing(t => t.AddSource("RetailPos.Sales.Worker.*"));

// Register the projection worker
builder.Services.AddHostedService<ProjectionWorker>();

// Infrastructure dependencies (injected via DI)
// builder.Services.AddScoped<IEventStore, PostgresEventStore>();
// builder.Services.AddScoped<ISaleReadModelRepository, EfCoreSaleReadModelRepository>();
// builder.Services.AddSingleton<IProjectionCheckpointStore, RedisCheckpointStore>();

builder.Services.AddHealthChecks()
    .AddCheck<WorkerHealthCheck>("projection-worker");

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();

// Health check exposes worker liveness to Kubernetes
public class WorkerHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Projection worker running."));
}
