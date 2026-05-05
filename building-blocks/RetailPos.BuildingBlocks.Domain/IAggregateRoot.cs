namespace RetailPos.BuildingBlocks.Domain;

public interface IEntity<TId>
{
    TId Id { get; }
}

public interface IAggregateRoot<TId> : IEntity<TId>
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
    int Version { get; }
}

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    string EventType { get; }
    int Version { get; }
}

public interface IVersionedEvent : IDomainEvent
{
    string SchemaVersion { get; }
    string CorrelationId { get; }
    string TenantId { get; }
}
