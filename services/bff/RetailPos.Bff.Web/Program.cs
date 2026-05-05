using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.OpenTelemetry().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());

// Typed HTTP clients — upstream microservices
builder.Services.AddHttpClient("sales",     c => c.BaseAddress = new Uri(builder.Configuration["Services:Sales:BaseUrl"]!)).AddStandardResilienceHandler();
builder.Services.AddHttpClient("inventory", c => c.BaseAddress = new Uri(builder.Configuration["Services:Inventory:BaseUrl"]!)).AddStandardResilienceHandler();
builder.Services.AddHttpClient("orders",    c => c.BaseAddress = new Uri(builder.Configuration["Services:Orders:BaseUrl"]!)).AddStandardResilienceHandler();

// Output cache for manager dashboard tiles (30-second refresh)
builder.Services.AddOutputCache(opts =>
    opts.AddPolicy("dashboard", p => p.Expire(TimeSpan.FromSeconds(30)).VaryByHeader("X-Tenant-Id")));

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseOutputCache();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
