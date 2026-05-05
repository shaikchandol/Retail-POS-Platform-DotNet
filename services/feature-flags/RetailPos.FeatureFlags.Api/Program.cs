using Microsoft.Extensions.Http.Resilience;
using RetailPos.BuildingBlocks.FeatureFlags;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.OpenTelemetry().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation());

// ── Feature Flag Storage Backend ──────────────────────────────────────────────
// Swap point: change IFeatureFlagProvider to use one of:
//   AzureAppConfigurationFeatureFlagProvider  → Azure App Config
//   LaunchDarklyFeatureFlagProvider           → LaunchDarkly
//   UnleashFeatureFlagProvider                → Unleash (self-hosted)
//   FlagsmithFeatureFlagProvider              → Flagsmith
// Interface contract stays stable — callers are unaffected.
builder.Services.AddSingleton<IFeatureFlagProvider, InMemoryFeatureFlagProvider>();
builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();

// Distributed cache: Redis (flags refresh every 5s — near real-time kill-switch)
builder.Services.AddStackExchangeRedisCache(opts =>
    opts.Configuration = builder.Configuration["Redis:ConnectionString"]);

builder.Services.AddAuthentication().AddJwtBearer(opts =>
{
    opts.Authority = builder.Configuration["Auth:Authority"];
    opts.Audience  = builder.Configuration["Auth:Audience"];
});
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

// In-memory provider (dev/test default)
public class InMemoryFeatureFlagProvider : IFeatureFlagProvider
{
    private readonly Dictionary<string, bool> _global = new()
    {
        [FeatureFlags.ApiV2Preview]             = false,
        [FeatureFlags.CheckoutSagaOrchestrator] = true,
        [FeatureFlags.OfflineMode]              = true,
        [FeatureFlags.AiPersonalization]        = false,
        [FeatureFlags.FraudDetectionRealtime]   = false,
        [FeatureFlags.DynamicPricing]           = false,
    };

    public Task<bool> GetValueAsync(string featureName, string? tenantId, CancellationToken ct) =>
        Task.FromResult(_global.GetValueOrDefault(featureName, false));

    public Task<IReadOnlyDictionary<string, bool>> GetAllAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, bool>>(_global);
}
