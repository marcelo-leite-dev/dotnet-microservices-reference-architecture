using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Inbox;

/// <summary>
/// Records that a consumer has processed a message. Written in the same transaction as the handler's
/// changes, it turns at-least-once delivery into effectively-once processing.
/// </summary>
public sealed class InboxMessage
{
    public required Guid MessageId { get; init; }

    public required string Consumer { get; init; }

    public required string Type { get; init; }

    public required DateTime ProcessedOnUtc { get; init; }
}

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(m => new { m.MessageId, m.Consumer });
        builder.Property(m => m.Consumer).HasMaxLength(256);
        builder.Property(m => m.Type).HasMaxLength(256);
    }
}
