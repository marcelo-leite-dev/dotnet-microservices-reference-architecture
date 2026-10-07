using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Infrastructure.Diagnostics;
using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Messaging;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace BuildingBlocks.Infrastructure.Kafka;

/// <summary>
/// Consumes every topic that has a registered integration event handler.
/// <list type="bullet">
/// <item>Offsets are committed manually, only after the message was handled (at-least-once).</item>
/// <item>The handler and the inbox record share one transaction (idempotent consumer).</item>
/// <item>Transient failures are retried with exponential backoff; poison messages go to <c>{topic}.dlq</c>.</item>
/// </list>
/// </summary>
internal sealed class KafkaConsumerService<TContext>(
    IServiceScopeFactory scopeFactory,
    IntegrationEventRegistry registry,
    IMessageProducer producer,
    IOptions<KafkaOptions> options,
    TimeProvider timeProvider,
    ILogger<KafkaConsumerService<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private readonly KafkaOptions _options = options.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        registry.Topics.Count == 0
            ? Task.CompletedTask
            : Task.Run(() => ConsumeLoopAsync(stoppingToken), stoppingToken);

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var config = _options.Apply(new ConsumerConfig
        {
            GroupId = _options.ConsumerGroup,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        });

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => logger.LogWarning("Kafka consumer error: {Reason}", error.Reason))
            .SetLogHandler((_, message) => logger.LogKafkaMessage(message))
            .Build();

        consumer.Subscribe(registry.Topics);
        logger.LogInformation("Kafka consumer {ConsumerGroup} subscribed to {Topics}", _options.ConsumerGroup, registry.Topics);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException exception)
                {
                    logger.LogError(exception, "Error consuming from Kafka");
                    continue;
                }

                if (result?.Message is null)
                {
                    continue;
                }

                await ProcessWithRetryAsync(result, stoppingToken);
                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown: uncommitted messages are redelivered to the next consumer.
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessWithRetryAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ProcessAsync(result, cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (attempt >= _options.MaxProcessingAttempts)
                {
                    logger.LogError(
                        exception,
                        "Message at {Topic}[{Partition}]@{Offset} failed after {Attempts} attempts; dead-lettering",
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        attempt);
                    await DeadLetterAsync(result, exception, cancellationToken);
                    return;
                }

                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt));
                logger.LogWarning(exception, "Processing attempt {Attempt} failed; retrying in {Delay}", attempt, delay);
                await Task.Delay(delay, timeProvider, cancellationToken);
            }
        }
    }

    private async Task ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        var headers = result.Message.Headers;
        var eventName = headers.GetString(MessageHeaderNames.EventType);

        if (eventName is null || !registry.TryGet(eventName, out var registration))
        {
            // Topics carry several event types; a service only handles the ones it registered.
            logger.LogDebug("Skipping unhandled event {EventType} from {Topic}", eventName, result.Topic);
            return;
        }

        if (!Guid.TryParse(headers.GetString(MessageHeaderNames.MessageId), out var messageId))
        {
            throw new InvalidOperationException($"Message at {result.TopicPartitionOffset} has no valid '{MessageHeaderNames.MessageId}' header.");
        }

        var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, headers, KafkaHeaderExtensions.GetValues);
        Baggage.Current = parentContext.Baggage;

        using var activity = MessagingDiagnostics.ActivitySource.StartActivity(
            $"{result.Topic} process",
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.destination.name", result.Topic);
        activity?.SetTag("messaging.message.id", messageId.ToString());
        activity?.SetTag("messaging.kafka.consumer.group", _options.ConsumerGroup);
        activity?.SetTag("messaging.kafka.message.offset", result.Offset.Value);
        activity?.SetTag("messaging.event_type", eventName);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var alreadyProcessed = await dbContext.Set<InboxMessage>()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == _options.ConsumerGroup, cancellationToken);

        if (alreadyProcessed)
        {
            logger.LogInformation("Message {MessageId} ({EventType}) already processed; skipping duplicate", messageId, eventName);
            return;
        }

        var integrationEvent = (IntegrationEvent?)JsonSerializer.Deserialize(result.Message.Value, registration.EventType, MessagingJson.Options)
            ?? throw new InvalidOperationException($"Message {messageId} has an empty payload.");

        await registration.Handle(scope.ServiceProvider, integrationEvent, cancellationToken);

        dbContext.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = messageId,
            Consumer = _options.ConsumerGroup,
            Type = eventName,
            ProcessedOnUtc = timeProvider.GetUtcNow().UtcDateTime,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        MessagingDiagnostics.MessagesProcessed.Add(
            1,
            new KeyValuePair<string, object?>("messaging.destination.name", result.Topic),
            new KeyValuePair<string, object?>("messaging.event_type", eventName));
    }

    private async Task DeadLetterAsync(ConsumeResult<string, string> result, Exception exception, CancellationToken cancellationToken)
    {
        var headers = result.Message.Headers;
        _ = Guid.TryParse(headers.GetString(MessageHeaderNames.MessageId), out var messageId);

        await producer.ProduceAsync(
            new OutgoingMessage(
                messageId,
                $"{result.Topic}.dlq",
                result.Message.Key ?? string.Empty,
                headers.GetString(MessageHeaderNames.EventType) ?? "unknown",
                result.Message.Value,
                Activity.Current?.Id,
                new Dictionary<string, string>
                {
                    [MessageHeaderNames.DeadLetterReason] = exception.Message,
                    [MessageHeaderNames.OriginalTopic] = result.Topic,
                }),
            cancellationToken);

        MessagingDiagnostics.MessagesDeadLettered.Add(1, new KeyValuePair<string, object?>("messaging.destination.name", result.Topic));
    }
}

internal static class KafkaHeaderExtensions
{
    public static string? GetString(this Headers? headers, string key) =>
        headers is not null && headers.TryGetLastBytes(key, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;

    public static IEnumerable<string> GetValues(Headers headers, string key) =>
        headers.GetString(key) is { } value ? [value] : [];
}
