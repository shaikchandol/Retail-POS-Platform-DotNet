using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.OpenTelemetry().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());

// Mobile BFF: extra response compression (mobile bandwidth is precious)
builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);

builder.Services.AddHttpClient("sales",   c => c.BaseAddress = new Uri(builder.Configuration["Services:Sales:BaseUrl"]!)).AddStandardResilienceHandler();
builder.Services.AddHttpClient("loyalty", c => c.BaseAddress = new Uri(builder.Configuration["Services:Loyalty:BaseUrl"]!)).AddStandardResilienceHandler();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseResponseCompression();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
