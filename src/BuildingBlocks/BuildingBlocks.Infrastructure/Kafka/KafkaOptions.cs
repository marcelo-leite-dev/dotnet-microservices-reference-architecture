using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;

namespace BuildingBlocks.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required]
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>Consumer group id; also the inbox "consumer" name used for de-duplication.</summary>
    [Required]
    public string ConsumerGroup { get; set; } = string.Empty;

    public string ClientId { get; set; } = Environment.MachineName;

    /// <summary>Create missing topics on startup. Disable for managed brokers where topics are provisioned as code.</summary>
    public bool CreateTopics { get; set; } = true;

    public int DefaultPartitions { get; set; } = 3;

    public short ReplicationFactor { get; set; } = 1;

    /// <summary>Topics this service publishes to (consumed topics are discovered from handler registrations).</summary>
    public string[] Topics { get; set; } = [];

    [Range(1, 20)]
    public int MaxProcessingAttempts { get; set; } = 3;

    /// <summary>
    /// Raw librdkafka properties applied last, e.g. <c>security.protocol</c> or <c>sasl.mechanism</c> for
    /// Azure Event Hubs / Confluent Cloud.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = [];

    internal TConfig Apply<TConfig>(TConfig config)
        where TConfig : ClientConfig
    {
        config.BootstrapServers = BootstrapServers;
        config.ClientId = ClientId;

        foreach (var (key, value) in Properties)
        {
            config.Set(key, value);
        }

        return config;
    }
}
