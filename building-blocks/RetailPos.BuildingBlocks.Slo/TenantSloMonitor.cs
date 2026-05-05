using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RetailPos.BuildingBlocks.Slo;

/// <summary>
/// Tenant-Level SLO Monitoring — measures and reports per-tenant SLOs.
///
/// SLO Dimensions (per tenant):
///   - Availability: % requests succeeding (target: 99.9%)
///   - P50/P95/P99 latency (target: p99 < 500ms for checkout)
///   - Error rate: % 5xx responses (target: < 0.1%)
///   - Event lag: time from event write to projection availability (target: < 500ms)
///
/// FinOps Cost Attribution:
///   - API call count per tenant (compute attribution)
///   - Event throughput per tenant (Kafka attribution)
///   - Storage growth per tenant (DB attribution)
///   - Showback: report cost per tenant (no chargeback in default model)
///
/// Swap point: export to Azure Monitor, Datadog, or Prometheus
///   by changing the MetricExporter — no monitor changes required.
/// </summary>
public class TenantSloMonitor
{
    private readonly Meter _meter = new("RetailPos.Slo", "1.0");
    private readonly Counter<long> _apiCallCounter;
    private readonly Histogram<double> _latencyHistogram;
    private readonly Counter<long> _errorCounter;
    private readonly Counter<long> _eventCounter;

    public TenantSloMonitor()
    {
        _apiCallCounter   = _meter.CreateCounter<long>("retailpos.api.calls",    "calls",  "Total API calls per tenant");
        _latencyHistogram = _meter.CreateHistogram<double>("retailpos.api.latency", "ms",  "API response latency per tenant");
        _errorCounter     = _meter.CreateCounter<long>("retailpos.api.errors",   "errors", "API errors per tenant");
        _eventCounter     = _meter.CreateCounter<long>("retailpos.events.published", "events", "Events published per tenant");
    }

    public void RecordApiCall(string tenantId, string service, string operation, double latencyMs, bool isError)
    {
        var tags = new TagList
        {
            ["tenant_id"] = tenantId,
            ["service"]   = service,
            ["operation"] = operation,
        };
        _apiCallCounter.Add(1, tags);
        _latencyHistogram.Record(latencyMs, tags);
        if (isError) _errorCounter.Add(1, tags);
    }

    public void RecordEvent(string tenantId, string eventType)
    {
        _eventCounter.Add(1, new TagList { ["tenant_id"] = tenantId, ["event_type"] = eventType });
    }
}

/// <summary>
/// Background worker that evaluates SLO compliance per tenant and emits alerts.
/// </summary>
public class SloEvaluationWorker(
    ITenantSloRepository sloRepo,
    ISloAlertPublisher alertPublisher,
    ILogger<SloEvaluationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await EvaluateAllTenantsAsync(ct);
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task EvaluateAllTenantsAsync(CancellationToken ct)
    {
        var tenants = await sloRepo.GetAllTenantIdsAsync(ct);
        foreach (var tenantId in tenants)
        {
            var slo = await sloRepo.GetCurrentSloAsync(tenantId, ct);
            if (slo.AvailabilityPercent < 99.9)
                await alertPublisher.PublishAsync(new SloAlert(tenantId, "AVAILABILITY", slo.AvailabilityPercent, target: 99.9), ct);
            if (slo.ErrorRatePercent > 0.1)
                await alertPublisher.PublishAsync(new SloAlert(tenantId, "ERROR_RATE", slo.ErrorRatePercent, target: 0.1), ct);
            if (slo.P99LatencyMs > 500)
                await alertPublisher.PublishAsync(new SloAlert(tenantId, "P99_LATENCY", slo.P99LatencyMs, target: 500), ct);
        }
    }
}

public record TenantSlo(string TenantId, double AvailabilityPercent, double ErrorRatePercent, double P99LatencyMs, double EventLagMs);
public record SloAlert(string TenantId, string Dimension, double CurrentValue, double Target);

public interface ITenantSloRepository
{
    Task<List<string>> GetAllTenantIdsAsync(CancellationToken ct);
    Task<TenantSlo> GetCurrentSloAsync(string tenantId, CancellationToken ct);
}
public interface ISloAlertPublisher { Task PublishAsync(SloAlert alert, CancellationToken ct); }
