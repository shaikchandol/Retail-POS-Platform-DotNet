using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.BuildingBlocks.Domain;

namespace RetailPos.Orders.Domain.Aggregates;

// ── Domain Events ─────────────────────────────────────────────────────────────
public record OrderCreatedEvent : IVersionedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => "order.created";
    public int Version { get; init; } = 1;
    public string SchemaVersion { get; init; } = "1.0";
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SaleId { get; init; }
    public required string CustomerId { get; init; }
    public required List<OrderLine> Lines { get; init; }
}

public record OrderFulfilledEvent : IVersionedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => "order.fulfilled";
    public int Version { get; init; } = 1;
    public string SchemaVersion { get; init; } = "1.0";
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required Guid OrderId { get; init; }
}

public record OrderLine(string ProductId, string Sku, int Quantity, decimal UnitPrice);

// ── Aggregate ─────────────────────────────────────────────────────────────────
public class Order : EventSourcedAggregate<Guid>
{
    public string TenantId { get; private set; } = string.Empty;
    public Guid SaleId { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public List<OrderLine> Lines { get; private set; } = [];

    private Order() { }

    public static Order Create(Guid orderId, Guid saleId, string tenantId, string customerId,
        List<OrderLine> lines, string correlationId)
    {
        var order = new Order();
        order.RaiseEvent(new OrderCreatedEvent
        {
            OrderId = orderId, SaleId = saleId, TenantId = tenantId,
            CustomerId = customerId, Lines = lines, CorrelationId = correlationId
        });
        return order;
    }

    public void Fulfil(string correlationId)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be fulfilled.");
        RaiseEvent(new OrderFulfilledEvent { OrderId = Id, TenantId = TenantId, CorrelationId = correlationId });
    }

    public void When(OrderCreatedEvent e)
    { Id = e.OrderId; TenantId = e.TenantId; SaleId = e.SaleId; CustomerId = e.CustomerId; Lines = e.Lines; Status = OrderStatus.Pending; }

    public void When(OrderFulfilledEvent e) => Status = OrderStatus.Fulfilled;
}

public enum OrderStatus { Pending, Fulfilled, Cancelled }
