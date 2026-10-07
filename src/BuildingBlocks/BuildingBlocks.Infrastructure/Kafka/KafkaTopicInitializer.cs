using BuildingBlocks.Messaging;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Kafka;

/// <summary>
/// Creates the topics a service publishes to and consumes from (plus their dead-letter topics) before
/// the consumer and outbox processor start. Meant for local and test environments; production topics
/// should be provisioned as code with explicit partitioning and retention.
/// </summary>
internal sealed class KafkaTopicInitializer(
    IntegrationEventRegistry registry,
    IOptions<KafkaOptions> options,
    ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    private const int MaxAttempts = 10;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.CreateTopics)
        {
            return;
        }

        var topics = settings.Topics
            .Concat(registry.Topics)
            .Concat(registry.Topics.Select(topic => $"{topic}.dlq"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        using var admin = new AdminClientBuilder(settings.Apply(new AdminClientConfig())).Build();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var existing = admin.GetMetadata(TimeSpan.FromSeconds(10)).Topics.Select(t => t.Topic).ToHashSet();
                var missing = topics.Where(topic => !existing.Contains(topic)).ToArray();

                if (missing.Length > 0)
                {
                    await admin.CreateTopicsAsync(missing.Select(topic => new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = settings.DefaultPartitions,
                        ReplicationFactor = settings.ReplicationFactor,
                    }));

                    logger.LogInformation("Created Kafka topics {Topics}", missing);
                }

                return;
            }
            catch (CreateTopicsException exception)
                when (exception.Results.All(r => r.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
            {
                return;
            }
            catch (KafkaException exception) when (attempt < MaxAttempts)
            {
                logger.LogWarning(exception, "Kafka not reachable yet (attempt {Attempt}/{MaxAttempts})", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
