using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RetailPos.BuildingBlocks.Offline;

/// <summary>
/// Store-and-Forward Worker — ASP.NET Core hosted background service.
/// Drains the local event buffer when connectivity to the cloud is available.
///
/// Sync strategy:
///   1. Check connectivity (HTTP ping to cloud endpoint)
///   2. Measure available bandwidth
///   3. Choose sync mode: streaming (high BW) | batch (low BW) | priority-only (very low BW)
///   4. Compress and upload events in priority order
///   5. Detect and resolve conflicts (server wins for pricing; store wins for sales)
///   6. Mark events as sent on success; keep on failure (retry next cycle)
/// </summary>
public class StoreAndForwardWorker(
    IEventBuffer buffer,
    ICloudSyncClient cloudClient,
    IConnectivityMonitor connectivity,
    IConflictResolver conflictResolver,
    ILogger<StoreAndForwardWorker> logger) : BackgroundService
{
    private static readonly TimeSpan OnlineCheckInterval  = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OfflineCheckInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StoreAndForwardWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var isOnline = await connectivity.IsOnlineAsync(stoppingToken);

            if (isOnline)
            {
                await SyncAsync(stoppingToken);
                await Task.Delay(OnlineCheckInterval, stoppingToken);
            }
            else
            {
                logger.LogWarning("Store offline — buffering events locally.");
                await Task.Delay(OfflineCheckInterval, stoppingToken);
            }
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        var pending = await buffer.GetPendingCountAsync(ct);
        if (pending == 0) return;

        var bw = await connectivity.MeasureBandwidthAsync(ct);
        var (batchSize, minPriority) = ResolveSyncStrategy(bw, pending);

        logger.LogInformation("Syncing {Pending} events. BW: {Bw}kbps. BatchSize: {Batch}. MinPriority: {Priority}",
            pending, bw, batchSize, minPriority);

        var events = await buffer.PeekAsync(batchSize, minPriority, ct);
        if (!events.Any()) return;

        // Compress batch
        var batch = new EventBatch(events, CompressedAt: DateTimeOffset.UtcNow, StoreId: events[0].StoreId);

        var result = await cloudClient.UploadBatchAsync(batch, ct);

        // Conflict resolution
        foreach (var conflict in result.Conflicts)
            await conflictResolver.ResolveAsync(conflict, ct);

        // Mark successfully uploaded events as sent
        var sentIds = result.AcceptedEventIds;
        await buffer.MarkSentAsync(sentIds, ct);

        logger.LogInformation("Synced {Count} events. Conflicts: {Conflicts}", sentIds.Count, result.Conflicts.Count);
    }

    private static (int BatchSize, EventPriority? MinPriority) ResolveSyncStrategy(int bandwidthKbps, int pending)
    {
        return bandwidthKbps switch
        {
            > 10_000 => (500, null),              // High BW: sync everything
            > 1_000  => (100, EventPriority.Inventory),  // Medium BW: skip telemetry
            _        => (20, EventPriority.Sale),  // Low BW: sales only
        };
    }
}

public record EventBatch(IReadOnlyList<BufferedEvent> Events, DateTimeOffset CompressedAt, string StoreId);

public record SyncResult(List<Guid> AcceptedEventIds, List<ConflictRecord> Conflicts);
public record ConflictRecord(Guid EventId, string ConflictType, string Resolution);

public interface ICloudSyncClient
{
    Task<SyncResult> UploadBatchAsync(EventBatch batch, CancellationToken ct);
}

public interface IConnectivityMonitor
{
    Task<bool> IsOnlineAsync(CancellationToken ct);
    Task<int> MeasureBandwidthAsync(CancellationToken ct);   // kbps
}

public interface IConflictResolver
{
    Task ResolveAsync(ConflictRecord conflict, CancellationToken ct);
}
