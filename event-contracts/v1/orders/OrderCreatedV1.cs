namespace RetailPos.EventContracts.V1.Orders;

public record OrderCreatedV1
{
    public const string TopicName = "retail.orders.events";
    public const string EventType = "order.created.v1";
    public const string SchemaVersion = "1.0";

    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SaleId { get; init; }
    public required string CustomerId { get; init; }
    public required string Status { get; init; }
    public required List<OrderLineV1> Lines { get; init; }
}

public record OrderLineV1(string ProductId, string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderFulfilledV1
{
    public const string EventType = "order.fulfilled.v1";
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid OrderId { get; init; }
}

public record OrderCancelledV1
{
    public const string EventType = "order.cancelled.v1";
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid OrderId { get; init; }
    public required string Reason { get; init; }
}
