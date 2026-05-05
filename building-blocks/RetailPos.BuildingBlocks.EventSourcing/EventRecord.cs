namespace RetailPos.BuildingBlocks.EventSourcing;

public class EventRecord
{
    public required string StreamId { get; init; }
    public required int Version { get; init; }
    public required string EventType { get; init; }
    public required string SchemaVersion { get; init; }
    public required string Payload { get; init; }           // JSON
    public required string Metadata { get; init; }          // JSON (tenantId, correlationId, causationId)
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Guid EventId { get; init; } = Guid.NewGuid();
}

public class EventMetadata
{
    public required string TenantId { get; init; }
    public required string CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string? UserId { get; init; }
    public string? TerminalId { get; init; }
    public string? StoreId { get; init; }
}
