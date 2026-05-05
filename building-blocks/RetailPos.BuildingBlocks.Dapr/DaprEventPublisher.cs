using Dapr.Client;
using RetailPos.BuildingBlocks.Domain;
using Microsoft.Extensions.Logging;

namespace RetailPos.BuildingBlocks.Dapr;

public class DaprEventPublisher(
    DaprClient daprClient,
    IEventTopicResolver topicResolver,
    ILogger<DaprEventPublisher> logger) : IDaprEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent @event, string? topic = null, CancellationToken ct = default)
        where TEvent : IVersionedEvent
    {
        var resolvedTopic = topic ?? topicResolver.ResolveTopicName(typeof(TEvent));
        var pubSub = topicResolver.ResolvePubSubName(typeof(TEvent));

        var metadata = new Dictionary<string, string>
        {
            ["tenant-id"] = @event.TenantId,
            ["correlation-id"] = @event.CorrelationId,
            ["event-type"] = @event.EventType,
            ["schema-version"] = @event.SchemaVersion,
            ["partition-key"] = @event.TenantId,   // Kafka partition by tenant
        };

        logger.LogInformation(
            "Publishing {EventType} v{SchemaVersion} to {PubSub}/{Topic} for tenant {TenantId}",
            @event.EventType, @event.SchemaVersion, pubSub, resolvedTopic, @event.TenantId);

        await daprClient.PublishEventAsync(pubSub, resolvedTopic, @event, metadata, ct);
    }

    public async Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, string? topic = null, CancellationToken ct = default)
        where TEvent : IVersionedEvent
    {
        foreach (var @event in events)
            await PublishAsync(@event, topic, ct);
    }
}

public class EventTopicResolver : IEventTopicResolver
{
    public string ResolveTopicName(Type eventType)
    {
        var attr = (EventTopicAttribute?)Attribute.GetCustomAttribute(eventType, typeof(EventTopicAttribute));
        return attr?.Topic ?? eventType.Name.ToLowerInvariant().Replace("event", "").TrimEnd('-');
    }

    public string ResolvePubSubName(Type eventType)
    {
        var attr = (EventTopicAttribute?)Attribute.GetCustomAttribute(eventType, typeof(EventTopicAttribute));
        return attr?.PubSub ?? "kafka-pubsub";
    }
}
