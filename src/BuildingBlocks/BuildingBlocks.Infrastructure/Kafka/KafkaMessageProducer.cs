using System.Diagnostics;
using System.Text;
using BuildingBlocks.Infrastructure.Diagnostics;
using BuildingBlocks.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace BuildingBlocks.Infrastructure.Kafka;

public sealed record OutgoingMessage(
    Guid MessageId,
    string Topic,
    string Key,
    string EventType,
    string Payload,
    string? TraceParent = null,
    IReadOnlyDictionary<string, string>? AdditionalHeaders = null);

public interface IMessageProducer
{
    Task ProduceAsync(OutgoingMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Idempotent Kafka producer (acks=all). Each publish is a PRODUCER span whose context is injected
/// into the message headers (W3C trace context), so the consumer continues the same trace.
/// </summary>
internal sealed class KafkaMessageProducer : IMessageProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaMessageProducer(IOptions<KafkaOptions> options, ILogger<KafkaMessageProducer> logger)
    {
        var config = options.Value.Apply(new ProducerConfig
        {
            Acks = Acks.All,
            EnableIdempotence = true,
            LingerMs = 5,
        });

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => logger.LogWarning("Kafka producer error: {Reason}", error.Reason))
            .SetLogHandler((_, message) => logger.LogKafkaMessage(message))
            .Build();
    }

    public async Task ProduceAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        ActivityContext.TryParse(message.TraceParent, null, isRemote: true, out var parentContext);

        using var activity = MessagingDiagnostics.ActivitySource.StartActivity(
            $"{message.Topic} publish",
            ActivityKind.Producer,
            parentContext);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("messaging.destination.name", message.Topic);
        activity?.SetTag("messaging.message.id", message.MessageId.ToString());
        activity?.SetTag("messaging.kafka.message.key", message.Key);
        activity?.SetTag("messaging.event_type", message.EventType);

        var headers = new Headers
        {
            { MessageHeaderNames.EventType, Encoding.UTF8.GetBytes(message.EventType) },
            { MessageHeaderNames.MessageId, Encoding.UTF8.GetBytes(message.MessageId.ToString()) },
        };

        foreach (var (key, value) in message.AdditionalHeaders ?? new Dictionary<string, string>())
        {
            headers.Add(key, Encoding.UTF8.GetBytes(value));
        }

        if (activity is not null)
        {
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                headers,
                static (carrier, key, value) => carrier.Add(key, Encoding.UTF8.GetBytes(value)));
        }

        await _producer.ProduceAsync(
            message.Topic,
            new Message<string, string> { Key = message.Key, Value = message.Payload, Headers = headers },
            cancellationToken);

        MessagingDiagnostics.MessagesPublished.Add(1, new KeyValuePair<string, object?>("messaging.destination.name", message.Topic));
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
