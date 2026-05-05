using RetailPos.BuildingBlocks.Domain;

namespace RetailPos.BuildingBlocks.Dapr;

/// <summary>
/// Dapr pub/sub abstraction. Dapr routes to Kafka in production,
/// in-memory transport in local dev, and test doubles in tests.
/// Cloud-agnostic: no Kafka SDK reference in domain or application layers.
/// </summary>
public interface IDaprEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, string? topic = null, CancellationToken ct = default)
        where TEvent : IVersionedEvent;

    Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, string? topic = null, CancellationToken ct = default)
        where TEvent : IVersionedEvent;
}

public interface IEventTopicResolver
{
    string ResolveTopicName(Type eventType);
    string ResolvePubSubName(Type eventType);
}

// Attribute to declare topic on event contract
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class EventTopicAttribute(string topic, string pubSub = "kafka-pubsub") : Attribute
{
    public string Topic { get; } = topic;
    public string PubSub { get; } = pubSub;
}
