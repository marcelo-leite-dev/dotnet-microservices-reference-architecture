using System.Text.Json;

namespace BuildingBlocks.Messaging;

/// <summary>
/// A fact published by one bounded context for others to react to. Integration events are part of a
/// service's public contract: they are versioned, serialized as JSON and delivered at-least-once.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Stores an integration event in the same transaction as the business change that produced it.
/// A background processor relays it to the broker afterwards (transactional outbox pattern).
/// </summary>
public interface IOutbox
{
    void Enqueue<TEvent>(TEvent integrationEvent, string topic, string partitionKey)
        where TEvent : IntegrationEvent;
}

public static class MessageHeaderNames
{
    public const string EventType = "event-type";
    public const string MessageId = "message-id";
    public const string DeadLetterReason = "dead-letter-reason";
    public const string OriginalTopic = "original-topic";
}

public static class IntegrationEventNames
{
    /// <summary>The wire name of an event, sent in the <see cref="MessageHeaderNames.EventType"/> header.</summary>
    public static string For(Type eventType) => eventType.Name;

    public static string For<TEvent>()
        where TEvent : IntegrationEvent => For(typeof(TEvent));
}

public static class MessagingJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
