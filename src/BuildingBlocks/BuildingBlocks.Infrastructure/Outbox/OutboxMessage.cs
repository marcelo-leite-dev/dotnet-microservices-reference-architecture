using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public const string TableName = "outbox_messages";

    public required Guid Id { get; init; }

    /// <summary>Wire name of the integration event (sent as the <c>event-type</c> header).</summary>
    public required string Type { get; init; }

    public required string Topic { get; init; }

    /// <summary>Kafka message key: guarantees ordering for messages of the same aggregate.</summary>
    public required string PartitionKey { get; init; }

    public required string Payload { get; init; }

    public required DateTime OccurredOnUtc { get; init; }

    /// <summary>W3C traceparent of the operation that produced the event, so the trace continues across the broker.</summary>
    public string? TraceParent { get; init; }

    public DateTime? ProcessedOnUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public void MarkProcessed(DateTime processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(OutboxMessage.TableName);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(256);
        builder.Property(m => m.Topic).HasMaxLength(256);
        builder.Property(m => m.PartitionKey).HasMaxLength(256);
        builder.Property(m => m.Payload).HasColumnType("jsonb");
        builder.Property(m => m.TraceParent).HasMaxLength(128);
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // Partial index: the processor only ever scans unprocessed messages.
        builder.HasIndex(m => m.OccurredOnUtc).HasFilter("processed_on_utc IS NULL");
    }
}
