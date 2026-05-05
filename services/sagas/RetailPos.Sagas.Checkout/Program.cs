using Dapr.Workflow;
using RetailPos.Sagas.Checkout.Activities;
using RetailPos.Sagas.Checkout.Orchestrators;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.OpenTelemetry().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation());

// ── Dapr Workflow (saga engine)
// Swap point: replace with MassTransit saga, NServiceBus, or Temporal
// by reimplementing the orchestrator as a StateMachine or WorkflowDef
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<CheckoutSagaOrchestrator>();
    options.RegisterActivity<ReserveInventoryActivity>();
    options.RegisterActivity<ReleaseInventoryActivity>();
    options.RegisterActivity<AuthorisePaymentActivity>();
    options.RegisterActivity<CompleteSaleActivity>();
    options.RegisterActivity<ConfirmInventoryActivity>();
});

builder.Services.AddDaprClient();

// Typed HTTP clients for downstream services
builder.Services.AddHttpClient<IInventorySagaClient, InventorySagaHttpClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:Inventory:BaseUrl"]!));
builder.Services.AddHttpClient<IPaymentSagaClient, PaymentSagaHttpClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:Payments:BaseUrl"]!));
builder.Services.AddHttpClient<ISalesSagaClient, SalesSagaHttpClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:Sales:BaseUrl"]!));

builder.Services.AddControllers().AddDapr();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseCloudEvents();
app.MapControllers();
app.MapSubscribeHandler();
app.MapHealthChecks("/health");
app.Run();

// ── Stub implementations (replace with real HTTP clients)
public class InventorySagaHttpClient(HttpClient http) : IInventorySagaClient
{
    public Task<(bool, string?)> ReserveStockAsync(string t, string p, int q, string key) => Task.FromResult((true, Guid.NewGuid().ToString()));
    public Task ReleaseReservationAsync(string t, string r, string reason, string key) => Task.CompletedTask;
    public Task ConfirmReservationAsync(string t, string r, string key) => Task.CompletedTask;
}
public class PaymentSagaHttpClient(HttpClient http) : IPaymentSagaClient
{
    public Task<(bool, string?, string?)> AuthoriseAsync(string t, Guid s, decimal a, string c, string m, string key) => Task.FromResult((true, "AUTH-001", (string?)null));
}
public class SalesSagaHttpClient(HttpClient http) : ISalesSagaClient
{
    public Task CompleteSaleAsync(string t, Guid s, string auth, string key) => Task.CompletedTask;
}
