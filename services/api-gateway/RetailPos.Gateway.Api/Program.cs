using Microsoft.AspNetCore.Authentication.JwtBearer;
using RetailPos.Gateway.Api.Middleware;
using RetailPos.Gateway.Api.Policies;
using Serilog;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.OpenTelemetry()
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());

// ── JWT Authentication ────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.Authority = builder.Configuration["Auth:Authority"];
        opts.Audience  = builder.Configuration["Auth:Audience"];
        opts.RequireHttpsMetadata = true;
    });

builder.Services.AddAuthorization();

// ── Policy Engine (Policy-as-Code) ────────────────────────────────────────────
builder.Services.AddSingleton<IGatewayPolicyProvider, YamlGatewayPolicyProvider>();
builder.Services.AddSingleton<IPolicyEvaluator, PolicyEvaluator>();

// ── Rate Limiting (tenant-aware) ──────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("tenant-rate-limit", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "anonymous",
            factory: _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 100
            }));
});

// ── YARP Reverse Proxy ────────────────────────────────────────────────────────
// YARP is Microsoft's open-source cloud-agnostic reverse proxy.
// Swap point: replace with Envoy, Nginx, Kong, or AGIC by changing this section
// and the matching YAML config — no business logic changes required.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseSerilogRequestLogging();
app.UseMiddleware<TenantIsolationMiddleware>();
app.UseMiddleware<PolicyEnforcementMiddleware>();
app.UseMiddleware<RequestTransformMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.MapHealthChecks("/health");

app.Run();
