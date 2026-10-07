using System.Diagnostics;
using System.Text.Json;
using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Adds integration events to the current DbContext. Nothing is sent to the broker here: the message
/// becomes durable only when the surrounding unit of work commits.
/// </summary>
internal sealed class EfOutbox<TContext>(TContext dbContext) : IOutbox
    where TContext : DbContext
{
    public void Enqueue<TEvent>(TEvent integrationEvent, string topic, string partitionKey)
        where TEvent : IntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = integrationEvent.Id,
            Type = IntegrationEventNames.For(integrationEvent.GetType()),
            Topic = topic,
            PartitionKey = partitionKey,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), MessagingJson.Options),
            OccurredOnUtc = integrationEvent.OccurredOnUtc,
            TraceParent = Activity.Current?.Id,
        });
    }
}
