using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Kafka;

internal static class KafkaLogging
{
    /// <summary>Routes librdkafka's internal log (otherwise written to stderr) through the structured logger.</summary>
    public static void LogKafkaMessage(this ILogger logger, LogMessage message)
    {
        var level = message.Level switch
        {
            SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical or SyslogLevel.Error => LogLevel.Error,
            SyslogLevel.Warning => LogLevel.Warning,
            SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
            _ => LogLevel.Debug,
        };

        logger.Log(level, "librdkafka [{KafkaClient}] {KafkaFacility}: {KafkaMessage}", message.Name, message.Facility, message.Message);
    }
}
