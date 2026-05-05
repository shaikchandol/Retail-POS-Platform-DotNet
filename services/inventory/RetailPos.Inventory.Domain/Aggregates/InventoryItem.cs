using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.BuildingBlocks.Domain;

namespace RetailPos.Inventory.Domain.Aggregates;

public record StockDeductedEvent : IVersionedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => "inventory.stock-deducted";
    public int Version { get; init; } = 1;
    public string SchemaVersion { get; init; } = "1.0";
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required string ProductId { get; init; }
    public required string Sku { get; init; }
    public required int QuantityDeducted { get; init; }
    public required int RemainingQuantity { get; init; }
}

public record StockReplenishedEvent : IVersionedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => "inventory.stock-replenished";
    public int Version { get; init; } = 1;
    public string SchemaVersion { get; init; } = "1.0";
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required string ProductId { get; init; }
    public required string Sku { get; init; }
    public required int QuantityAdded { get; init; }
    public required int NewTotal { get; init; }
}

public class InventoryItem : EventSourcedAggregate<string>
{
    public string TenantId { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string StoreId { get; private set; } = string.Empty;
    public int QuantityOnHand { get; private set; }
    public int ReorderThreshold { get; private set; }
    public bool IsLowStock => QuantityOnHand <= ReorderThreshold;

    private InventoryItem() { }

    public void Deduct(int quantity, string correlationId)
    {
        if (quantity > QuantityOnHand)
            throw new InvalidOperationException($"Insufficient stock. Available: {QuantityOnHand}, Requested: {quantity}");
        RaiseEvent(new StockDeductedEvent
        {
            TenantId = TenantId, ProductId = Id, Sku = Sku,
            QuantityDeducted = quantity, RemainingQuantity = QuantityOnHand - quantity, CorrelationId = correlationId
        });
    }

    public void Replenish(int quantity, string correlationId) =>
        RaiseEvent(new StockReplenishedEvent
        {
            TenantId = TenantId, ProductId = Id, Sku = Sku,
            QuantityAdded = quantity, NewTotal = QuantityOnHand + quantity, CorrelationId = correlationId
        });

    public void When(StockDeductedEvent e) => QuantityOnHand = e.RemainingQuantity;
    public void When(StockReplenishedEvent e) => QuantityOnHand = e.NewTotal;
}
