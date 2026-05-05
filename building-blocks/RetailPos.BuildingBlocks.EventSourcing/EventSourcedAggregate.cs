using RetailPos.BuildingBlocks.Domain;

namespace RetailPos.BuildingBlocks.EventSourcing;

public abstract class EventSourcedAggregate<TId> : IAggregateRoot<TId>
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];
    private readonly List<IDomainEvent> _history = [];

    public TId Id { get; protected set; } = default!;
    public int Version { get; private set; } = -1;
    public IReadOnlyList<IDomainEvent> DomainEvents => _uncommittedEvents.AsReadOnly();

    // Apply an event (raises + records)
    protected void RaiseEvent(IDomainEvent @event)
    {
        ApplyEvent(@event);
        _uncommittedEvents.Add(@event);
    }

    // Replay from event store (reconstitute state)
    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var @event in history)
        {
            ApplyEvent(@event);
            _history.Add(@event);
            Version++;
        }
    }

    // Dispatch to When() overloads via dynamic dispatch
    private void ApplyEvent(IDomainEvent @event)
    {
        var method = GetType().GetMethod("When", [@event.GetType()]);
        if (method is null)
            throw new InvalidOperationException(
                $"Aggregate '{GetType().Name}' does not handle event '{@event.GetType().Name}'.");
        method.Invoke(this, [@event]);
    }

    public void ClearDomainEvents() => _uncommittedEvents.Clear();
    public IReadOnlyList<IDomainEvent> GetHistory() => _history.AsReadOnly();
}

public interface IEventStore
{
    Task SaveEventsAsync(string streamId, IEnumerable<IDomainEvent> events, int expectedVersion, CancellationToken ct = default);
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(string streamId, int fromVersion = 0, CancellationToken ct = default);
    Task<bool> StreamExistsAsync(string streamId, CancellationToken ct = default);
}

public interface ISnapshotStore
{
    Task SaveSnapshotAsync<T>(string streamId, T snapshot, int version, CancellationToken ct = default);
    Task<(T? Snapshot, int Version)> GetSnapshotAsync<T>(string streamId, CancellationToken ct = default);
}
