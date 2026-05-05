namespace RetailPos.EventContracts.V1.Sales;

/// <summary>
/// Immutable, versioned integration event published to Kafka topic: retail.sales.events
/// Consumers MUST handle unknown fields gracefully (forward compatibility).
/// Breaking changes require a new event type (e.g., SaleCreatedV2).
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public record SaleCreatedV1
{
    public const string TopicName = "retail.sales.events";
    public const string SchemaVersion = "1.0";
    public const string EventType = "sale.created.v1";

    public required Guid EventId { get; init; }
    public required string EventType_ => EventType;
    public required string SchemaVersion_ => SchemaVersion;
    public required DateTimeOffset OccurredAt { get; init; }

    // Tenant context
    public required string TenantId { get; init; }
    public required string StoreId { get; init; }
    public required string TerminalId { get; init; }
    public required string CorrelationId { get; init; }

    // Business payload
    public required Guid SaleId { get; init; }
    public required string CustomerId { get; init; }
    public required string ReceiptNumber { get; init; }
    public required string PaymentMethod { get; init; }
    public required string Currency { get; init; }
    public required decimal SubTotal { get; init; }
    public required decimal TaxTotal { get; init; }
    public required decimal DiscountTotal { get; init; }
    public required decimal GrandTotal { get; init; }
    public required List<SaleLineItemV1> LineItems { get; init; }
}

public record SaleLineItemV1
{
    public required string ProductId { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
}

public record SaleVoidedV1
{
    public const string TopicName = "retail.sales.events";
    public const string EventType = "sale.voided.v1";

    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid SaleId { get; init; }
    public required string Reason { get; init; }
    public string? AuthorizedBy { get; init; }
}

public record SaleRefundedV1
{
    public const string TopicName = "retail.sales.events";
    public const string EventType = "sale.refunded.v1";

    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid SaleId { get; init; }
    public required decimal RefundAmount { get; init; }
    public required string Currency { get; init; }
    public required string Reason { get; init; }
}
