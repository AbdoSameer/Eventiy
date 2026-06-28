using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;
public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);


        builder.Property(x => x.EventName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Domain)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.OccurredOnUtc)
            .IsRequired();

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.ProcessedOnUtc);
        builder.Property(x => x.NextRetryOnUtc);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ProcessingLock);
        builder.Property(x => x.ProcessingLockedAt);


        builder.HasIndex(x => new { x.ProcessedOnUtc, x.NextRetryOnUtc })
            .HasDatabaseName("IX_OutboxMessages_ReadyForProcessing");

        builder.HasIndex(x => x.ProcessedOnUtc)
            .HasDatabaseName("IX_OutboxMessages_ProcessedOnUtc");

        builder.HasIndex(x => x.Domain)
            .HasDatabaseName("IX_OutboxMessages_Domain");

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_OutboxMessages_IdempotencyKey");

        builder.HasIndex(x => new { x.ProcessingLock, x.ProcessingLockedAt })
            .HasDatabaseName("IX_OutboxMessages_Processing");
    }
}