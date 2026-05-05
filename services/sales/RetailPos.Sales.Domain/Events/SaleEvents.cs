using RetailPos.BuildingBlocks.Domain;

namespace RetailPos.Sales.Domain.Events;

public abstract record SaleDomainEvent : IVersionedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public abstract string EventType { get; }
    public int Version { get; init; } = 1;
    public string SchemaVersion { get; init; } = "1.0";
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public string? StoreId { get; init; }
    public string? TerminalId { get; init; }
    public string? CashierId { get; init; }
}

public record SaleInitiatedEvent : SaleDomainEvent
{
    public override string EventType => "sale.initiated";
    public required Guid SaleId { get; init; }
    public required string CustomerId { get; init; }
    public required string Currency { get; init; }
}

public record SaleItemAddedEvent : SaleDomainEvent
{
    public override string EventType => "sale.item-added";
    public required Guid SaleId { get; init; }
    public required string ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required string Currency { get; init; }
}

public record SaleItemRemovedEvent : SaleDomainEvent
{
    public override string EventType => "sale.item-removed";
    public required Guid SaleId { get; init; }
    public required string ProductId { get; init; }
}

public record SaleItemQuantityUpdatedEvent : SaleDomainEvent
{
    public override string EventType => "sale.item-quantity-updated";
    public required Guid SaleId { get; init; }
    public required string ProductId { get; init; }
    public required int NewQuantity { get; init; }
}

public record DiscountAppliedEvent : SaleDomainEvent
{
    public override string EventType => "sale.discount-applied";
    public required Guid SaleId { get; init; }
    public required string DiscountCode { get; init; }
    public required decimal DiscountPercentage { get; init; }
}

public record SaleCompletedEvent : SaleDomainEvent
{
    public override string EventType => "sale.completed";
    public required Guid SaleId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal TaxTotal { get; init; }
    public required decimal DiscountTotal { get; init; }
    public required string Currency { get; init; }
    public required string PaymentMethod { get; init; }
    public string? ReceiptNumber { get; init; }
}

public record SaleVoidedEvent : SaleDomainEvent
{
    public override string EventType => "sale.voided";
    public required Guid SaleId { get; init; }
    public required string Reason { get; init; }
    public string? AuthorizedBy { get; init; }
}

public record SaleRefundedEvent : SaleDomainEvent
{
    public override string EventType => "sale.refunded";
    public required Guid SaleId { get; init; }
    public required decimal RefundAmount { get; init; }
    public required string Currency { get; init; }
    public required string Reason { get; init; }
}
