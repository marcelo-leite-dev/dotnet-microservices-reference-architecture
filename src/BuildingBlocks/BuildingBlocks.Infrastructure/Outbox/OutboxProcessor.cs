using BuildingBlocks.Infrastructure.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 50;
}

/// <summary>
/// Relays committed outbox messages to Kafka. Rows are claimed with <c>FOR UPDATE SKIP LOCKED</c>, so
/// several replicas can run the processor concurrently without publishing the same row twice in
/// parallel. Delivery is at-least-once: consumers must be idempotent (see the inbox).
/// </summary>
internal sealed class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IMessageProducer producer,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        using var timer = new PeriodicTimer(settings.PollingInterval, timeProvider);

        try
        {
            do
            {
                try
                {
                    // Drain the backlog quickly, then go back to polling.
                    while (await PublishBatchAsync(settings.BatchSize, stoppingToken) == settings.BatchSize)
                    {
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Outbox processing for {DbContext} failed", typeof(TContext).Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
    }

    private async Task<int> PublishBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await dbContext.Set<OutboxMessage>()
            .FromSql($"""
                      SELECT * FROM outbox_messages
                      WHERE processed_on_utc IS NULL
                      ORDER BY occurred_on_utc
                      LIMIT {batchSize}
                      FOR UPDATE SKIP LOCKED
                      """)
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var message in messages)
        {
            try
            {
                await producer.ProduceAsync(
                    new OutgoingMessage(message.Id, message.Topic, message.PartitionKey, message.Type, message.Payload, message.TraceParent),
                    cancellationToken);

                message.MarkProcessed(timeProvider.GetUtcNow().UtcDateTime);
                published++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Stop at the first failure to preserve per-key ordering; the row is retried next tick.
                message.MarkFailed(exception.Message);
                logger.LogWarning(exception, "Failed to publish outbox message {MessageId} ({EventType})", message.Id, message.Type);
                break;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return published;
    }
}
