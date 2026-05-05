namespace RetailPos.BuildingBlocks.Offline;

/// <summary>
/// Retail Offline-First: Event Buffer for store-to-cloud synchronization.
///
/// When the store loses connectivity:
///   1. POS terminal writes to LOCAL event buffer (SQLite / local PostgreSQL)
///   2. Terminal continues operating normally (checkout, payments, inventory)
///   3. When connectivity restores, StoreAndForwardWorker drains the buffer
///   4. Conflict detection runs during sync (clock skew, duplicate events)
///
/// Priority queues: sales events > inventory > telemetry
/// Compression: events are batch-compressed before upload (gzip)
/// Bandwidth: configurable adaptive sync (low BW = batch only, high BW = streaming)
/// </summary>
public interface IEventBuffer
{
    Task EnqueueAsync(BufferedEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<BufferedEvent>> PeekAsync(int maxEvents, EventPriority? minPriority = null, CancellationToken ct = default);
    Task MarkSentAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

public class SqliteEventBuffer(ILocalDb db, ILogger<SqliteEventBuffer> logger) : IEventBuffer
{
    public async Task EnqueueAsync(BufferedEvent evt, CancellationToken ct = default)
    {
        await db.ExecuteAsync(
            "INSERT INTO event_buffer (id, event_type, payload, priority, tenant_id, created_at, sent) VALUES (?,?,?,?,?,?,0)",
            evt.Id, evt.EventType, evt.Payload, (int)evt.Priority, evt.TenantId, evt.CreatedAt);
        logger.LogDebug("Buffered event {EventType} [{Priority}]", evt.EventType, evt.Priority);
    }

    public async Task<IReadOnlyList<BufferedEvent>> PeekAsync(int maxEvents, EventPriority? minPriority = null, CancellationToken ct = default)
    {
        var sql = minPriority.HasValue
            ? $"SELECT * FROM event_buffer WHERE sent=0 AND priority >= {(int)minPriority} ORDER BY priority DESC, created_at ASC LIMIT {maxEvents}"
            : $"SELECT * FROM event_buffer WHERE sent=0 ORDER BY priority DESC, created_at ASC LIMIT {maxEvents}";
        return await db.QueryAsync<BufferedEvent>(sql);
    }

    public async Task MarkSentAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default)
    {
        var ids = string.Join(",", eventIds.Select(id => $"'{id}'"));
        await db.ExecuteAsync($"UPDATE event_buffer SET sent=1, sent_at=datetime('now') WHERE id IN ({ids})");
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default) =>
        await db.ScalarAsync<int>("SELECT COUNT(*) FROM event_buffer WHERE sent=0");

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default) =>
        await db.PingAsync();
}

public record BufferedEvent(
    Guid Id,
    string EventType,
    string Payload,      // JSON
    EventPriority Priority,
    string TenantId,
    string StoreId,
    DateTimeOffset CreatedAt,
    bool Sent = false,
    DateTimeOffset? SentAt = null
);

public enum EventPriority
{
    Telemetry = 10,      // Lowest: operational metrics
    Inventory = 50,      // Medium: stock updates
    Payment   = 80,      // High: payment events
    Sale      = 100,     // Highest: must sync first
}

public interface ILocalDb
{
    Task ExecuteAsync(string sql, params object[] args);
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql);
    Task<T> ScalarAsync<T>(string sql);
    Task<bool> PingAsync();
}
