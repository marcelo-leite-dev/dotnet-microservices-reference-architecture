using System.Text;
using System.Text.Json;
using BuildingBlocks.Messaging;
using Confluent.Kafka;

namespace Testing.Shared;

/// <summary>Publishes and observes integration events on a real broker, exactly as the services do.</summary>
public sealed class KafkaTestClient(string bootstrapServers)
{
    public async Task PublishAsync<TEvent>(string topic, string key, TEvent integrationEvent)
        where TEvent : IntegrationEvent
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build();

        await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = JsonSerializer.Serialize(integrationEvent, MessagingJson.Options),
            Headers = new Headers
            {
                { MessageHeaderNames.EventType, Encoding.UTF8.GetBytes(IntegrationEventNames.For<TEvent>()) },
                { MessageHeaderNames.MessageId, Encoding.UTF8.GetBytes(integrationEvent.Id.ToString()) },
            },
        });
    }

    /// <summary>Reads the topic from the beginning until an event matching <paramref name="predicate"/> appears.</summary>
    public Task<TEvent?> WaitForEventAsync<TEvent>(string topic, Func<TEvent, bool> predicate, TimeSpan timeout)
        where TEvent : IntegrationEvent => Task.Run(() =>
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        var expectedType = IntegrationEventNames.For<TEvent>();
        var deadline = DateTime.UtcNow + timeout;

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
                if (result?.Message is null ||
                    !result.Message.Headers.TryGetLastBytes(MessageHeaderNames.EventType, out var typeBytes) ||
                    Encoding.UTF8.GetString(typeBytes) != expectedType)
                {
                    continue;
                }

                var integrationEvent = JsonSerializer.Deserialize<TEvent>(result.Message.Value, MessagingJson.Options);
                if (integrationEvent is not null && predicate(integrationEvent))
                {
                    return integrationEvent;
                }
            }
        }
        finally
        {
            consumer.Close();
        }

        return null;
    });
}
