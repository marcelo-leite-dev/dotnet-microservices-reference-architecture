using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BuildingBlocks.Infrastructure.Diagnostics;

public static class MessagingDiagnostics
{
    public const string Name = "BuildingBlocks.Messaging";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> MessagesPublished = Meter.CreateCounter<long>(
        "messaging.publish.messages",
        description: "Messages published to the broker by the outbox processor.");

    public static readonly Counter<long> MessagesProcessed = Meter.CreateCounter<long>(
        "messaging.process.messages",
        description: "Messages successfully processed by integration event handlers.");

    public static readonly Counter<long> MessagesDeadLettered = Meter.CreateCounter<long>(
        "messaging.dead_letter.messages",
        description: "Messages moved to a dead-letter topic after exhausting retries.");
}
