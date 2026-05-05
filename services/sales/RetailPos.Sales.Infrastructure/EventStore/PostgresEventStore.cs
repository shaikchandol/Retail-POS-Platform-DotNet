using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailPos.BuildingBlocks.Domain;
using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.Sales.Domain.Exceptions;

namespace RetailPos.Sales.Infrastructure.EventStore;

/// <summary>
/// PostgreSQL-backed event store using EF Core.
/// Schema is tenant-scoped via search_path.
/// Optimistic concurrency via expectedVersion check.
/// </summary>
public class PostgresEventStore(EventStoreDbContext db) : IEventStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private static readonly Dictionary<string, Type> EventTypeMap = new()
    {
        ["sale.initiated"] = typeof(RetailPos.Sales.Domain.Events.SaleInitiatedEvent),
        ["sale.item-added"] = typeof(RetailPos.Sales.Domain.Events.SaleItemAddedEvent),
        ["sale.item-removed"] = typeof(RetailPos.Sales.Domain.Events.SaleItemRemovedEvent),
        ["sale.item-quantity-updated"] = typeof(RetailPos.Sales.Domain.Events.SaleItemQuantityUpdatedEvent),
        ["sale.discount-applied"] = typeof(RetailPos.Sales.Domain.Events.DiscountAppliedEvent),
        ["sale.completed"] = typeof(RetailPos.Sales.Domain.Events.SaleCompletedEvent),
        ["sale.voided"] = typeof(RetailPos.Sales.Domain.Events.SaleVoidedEvent),
        ["sale.refunded"] = typeof(RetailPos.Sales.Domain.Events.SaleRefundedEvent),
    };

    public async Task SaveEventsAsync(string streamId, IEnumerable<IDomainEvent> events, int expectedVersion, CancellationToken ct = default)
    {
        var eventList = events.ToList();
        if (!eventList.Any()) return;

        var currentVersion = await db.Events
            .Where(e => e.StreamId == streamId)
            .MaxAsync(e => (int?)e.Version, ct) ?? -1;

        if (currentVersion != expectedVersion)
            throw new ConcurrencyException(streamId, expectedVersion, currentVersion);

        var version = currentVersion;
        foreach (var evt in eventList)
        {
            version++;
            db.Events.Add(new EventRecord
            {
                StreamId = streamId,
                Version = version,
                EventType = evt.EventType,
                SchemaVersion = evt is IVersionedEvent ve ? ve.SchemaVersion : "1.0",
                Payload = JsonSerializer.Serialize(evt, evt.GetType(), JsonOpts),
                Metadata = JsonSerializer.Serialize(new { evt.EventId, evt.OccurredAt }),
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(string streamId, int fromVersion = 0, CancellationToken ct = default)
    {
        var records = await db.Events
            .Where(e => e.StreamId == streamId && e.Version >= fromVersion)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        return records.Select(Deserialize).Where(e => e is not null).Cast<IDomainEvent>();
    }

    public async Task<bool> StreamExistsAsync(string streamId, CancellationToken ct = default) =>
        await db.Events.AnyAsync(e => e.StreamId == streamId, ct);

    private static IDomainEvent? Deserialize(EventRecord record)
    {
        if (!EventTypeMap.TryGetValue(record.EventType, out var type)) return null;
        return (IDomainEvent?)JsonSerializer.Deserialize(record.Payload, type, JsonOpts);
    }
}

public class EventStoreDbContext(DbContextOptions<EventStoreDbContext> options) : DbContext(options)
{
    public DbSet<EventRecord> Events => Set<EventRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<EventRecord>(e =>
        {
            e.ToTable("event_store");
            e.HasKey(x => new { x.StreamId, x.Version });
            e.Property(x => x.StreamId).HasMaxLength(200);
            e.Property(x => x.EventType).HasMaxLength(100);
            e.Property(x => x.SchemaVersion).HasMaxLength(20);
            e.HasIndex(x => x.StreamId);
            e.HasIndex(x => new { x.EventType, x.CreatedAt });
        });
    }
}
