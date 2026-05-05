using RetailPos.BuildingBlocks.EventSourcing;
using RetailPos.BuildingBlocks.Domain;

namespace RetailPos.Payments.Domain.Aggregates;

public record PaymentInitiatedEvent : IVersionedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => "payment.initiated";
    public int Version { get; init; } = 1;
    public string SchemaVersion { get; init; } = "1.0";
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid SaleId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Method { get; init; }
}

public record PaymentAuthorisedEvent : IVersionedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => "payment.authorised";
    public int Version { get; init; } = 1;
    public string SchemaVersion { get; init; } = "1.0";
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required Guid PaymentId { get; init; }
    public required string AuthCode { get; init; }
}

public class Payment : EventSourcedAggregate<Guid>
{
    public string TenantId { get; private set; } = string.Empty;
    public Guid SaleId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string Method { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }

    private Payment() { }

    public static Payment Initiate(Guid paymentId, Guid saleId, string tenantId, decimal amount, string currency, string method, string correlationId)
    {
        var p = new Payment();
        p.RaiseEvent(new PaymentInitiatedEvent { PaymentId = paymentId, SaleId = saleId, TenantId = tenantId, Amount = amount, Currency = currency, Method = method, CorrelationId = correlationId });
        return p;
    }

    public void Authorise(string authCode, string correlationId)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Cannot authorise a non-pending payment.");
        RaiseEvent(new PaymentAuthorisedEvent { PaymentId = Id, TenantId = TenantId, AuthCode = authCode, CorrelationId = correlationId });
    }

    public void When(PaymentInitiatedEvent e) { Id = e.PaymentId; TenantId = e.TenantId; SaleId = e.SaleId; Amount = e.Amount; Currency = e.Currency; Method = e.Method; Status = PaymentStatus.Pending; }
    public void When(PaymentAuthorisedEvent e) => Status = PaymentStatus.Authorised;
}

public enum PaymentStatus { Pending, Authorised, Declined, Refunded }
