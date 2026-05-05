namespace RetailPos.EventContracts.V1.Payments;

public record PaymentAuthorisedV1
{
    public const string TopicName = "retail.payments.events";
    public const string EventType = "payment.authorised.v1";

    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid SaleId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Method { get; init; }          // CARD, CASH, QR, WALLET
    public required string AuthCode { get; init; }
    public string? Last4 { get; init; }
}

public record PaymentDeclinedV1
{
    public const string EventType = "payment.declined.v1";
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid SaleId { get; init; }
    public required string Reason { get; init; }
    public required string DeclineCode { get; init; }
}

public record RefundProcessedV1
{
    public const string EventType = "payment.refund-processed.v1";
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public required Guid RefundId { get; init; }
    public required Guid OriginalPaymentId { get; init; }
    public required decimal RefundAmount { get; init; }
    public required string Currency { get; init; }
}
