using FluentValidation;
using MediatR;
using RetailPos.BuildingBlocks.Dapr;
using RetailPos.BuildingBlocks.MultiTenancy;
using RetailPos.Sales.Application.Behaviors;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {TenantId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.OpenTelemetry()
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddSource("RetailPos.*"));

// ── Dapr ──────────────────────────────────────────────────────────────────────
builder.Services.AddDaprClient();
builder.Services.AddSingleton<IEventTopicResolver, EventTopicResolver>();
builder.Services.AddScoped<IDaprEventPublisher, DaprEventPublisher>();

// ── Multi-Tenancy ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ── MediatR + Pipelines ───────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RetailPos.Sales.Application.Features.CreateSale.CreateSaleHandler).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(RetailPos.Sales.Application.Features.CreateSale.CreateSaleValidator).Assembly);

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers().AddDapr();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "RetailPOS Sales Service", Version = "v1" });
    c.AddSecurityDefinition("TenantHeader", new() { Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey, In = Microsoft.OpenApi.Models.ParameterLocation.Header, Name = "X-Tenant-Id" });
});

// ── Health ────────────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();

app.UseMiddleware<TenantMiddleware>();
app.UseSerilogRequestLogging();
app.UseCloudEvents();
app.UseRouting();
app.MapControllers();
app.MapSubscribeHandler();
app.MapHealthChecks("/health");
app.UseSwagger();
app.UseSwaggerUI();
app.Run();
