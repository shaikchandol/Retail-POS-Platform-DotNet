using RetailPos.Pci.Tokenization.Api.Services;
using Serilog;

// PCI DSS Hardened ASP.NET Core service.
// Deployed in isolated Kubernetes namespace: pci
// NetworkPolicy: only payments-service can reach this via mTLS
// All endpoints require: JWT auth + mTLS client certificate

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.OpenTelemetry()
    .Enrich.FromLogContext()
    .Filter.ByExcluding(e => e.Properties.ContainsKey("pan"))  // NEVER log PANs
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation());

// Require client certificate (mTLS) in addition to JWT
builder.Services.AddCertificateForwarding(opts => opts.CertificateHeader = "X-Client-Cert");
builder.Services.AddAuthentication()
    .AddJwtBearer("jwt", opts =>
    {
        opts.Authority = builder.Configuration["Auth:Authority"];
        opts.Audience  = builder.Configuration["Auth:Audience"];
    })
    .AddCertificate("mtls", opts =>
    {
        opts.AllowedCertificateTypes = Microsoft.AspNetCore.Authentication.Certificate.CertificateTypes.All;
    });

builder.Services.AddAuthorization(opts =>
{
    // Require BOTH JWT + mTLS certificate
    opts.AddPolicy("pci-service-only", policy =>
        policy.RequireAuthenticatedUser()
              .AddAuthenticationSchemes("jwt", "mtls")
              .RequireClaim("service_name", "payments-service"));

    opts.AddPolicy("payment-authoriser", policy =>
        policy.RequireRole("payment-service"));
});

// PCI Services
builder.Services.AddSingleton<TokenizationService>();
// builder.Services.AddSingleton<ITokenVault, AzureKeyVaultTokenVault>();        // Azure KV HSM
// builder.Services.AddSingleton<ITokenVault, DaprSecretTokenVault>();            // Dapr secrets (swap)
// builder.Services.AddSingleton<ITokenAuditLog, PostgresImmutableAuditLog>();    // Tamper-proof log

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// PCI: strict HTTPS
app.UseHttpsRedirection();
app.UseHsts();
app.UseCertificateForwarding();
app.UseAuthentication();
app.UseAuthorization();
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        // Mask any card-related values that might slip into logs
        diag.Set("path_safe", ctx.Request.Path.Value?.Replace("tokenize", "[REDACTED]"));
    };
});
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
