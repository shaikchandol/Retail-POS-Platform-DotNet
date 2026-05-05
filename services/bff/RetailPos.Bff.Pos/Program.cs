using Microsoft.Extensions.Http.Resilience;
using RetailPos.Bff.Pos.Controllers;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.OpenTelemetry().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());

// ── Typed HTTP clients with resilience (Microsoft.Extensions.Http.Resilience)
// Swap point: replace resilience handler with Polly, Steeltoe, or Dapr service invocation
builder.Services.AddHttpClient<ISalesServiceClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:Sales:BaseUrl"]!))
    .AddStandardResilienceHandler();  // Circuit breaker + retry + timeout built in

builder.Services.AddHttpClient<IPricingServiceClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:Pricing:BaseUrl"]!))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IInventoryServiceClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:Inventory:BaseUrl"]!))
    .AddStandardResilienceHandler();

// ── Response caching for product catalogue (reduces downstream load for offline scenarios)
builder.Services.AddResponseCaching();
builder.Services.AddOutputCache(opts =>
{
    opts.AddPolicy("product-cache", p => p.Expire(TimeSpan.FromMinutes(15)));
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseResponseCaching();
app.UseOutputCache();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
