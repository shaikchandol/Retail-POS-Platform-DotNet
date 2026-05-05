namespace RetailPos.EventContracts.V1.Inventory;

public record StockReservedV1
{
    public const string TopicName = "retail.inventory.events";
    public const string EventType = "inventory.stock-reserved.v1";

    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid ReservationId { get; init; }
    public required Guid OrderId { get; init; }
    public required string ProductId { get; init; }
    public required string Sku { get; init; }
    public required int QuantityReserved { get; init; }
    public required string StoreId { get; init; }
}

public record StockReleasedV1
{
    public const string EventType = "inventory.stock-released.v1";
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid ReservationId { get; init; }
    public required string Reason { get; init; }
}

public record StockLevelUpdatedV1
{
    public const string EventType = "inventory.stock-level-updated.v1";
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required string ProductId { get; init; }
    public required string Sku { get; init; }
    public required int NewQuantity { get; init; }
    public required int PreviousQuantity { get; init; }
    public required string StoreId { get; init; }
}

public record LowStockAlertV1
{
    public const string EventType = "inventory.low-stock-alert.v1";
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required string ProductId { get; init; }
    public required int CurrentQuantity { get; init; }
    public required int ReorderThreshold { get; init; }
}
