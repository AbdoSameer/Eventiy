using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

public sealed class CompensationLogConfiguration : IEntityTypeConfiguration<CompensationLog>
{
    public void Configure(EntityTypeBuilder<CompensationLog> builder)
    {
        builder.ToTable("CompensationLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId)
            .IsRequired();

        builder.Property(x => x.CompensationType)
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

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ProcessingLock);
        builder.Property(x => x.ProcessingLockedAt);

        builder.HasIndex(x => new { x.ProcessedOnUtc, x.NextRetryOnUtc })
            .HasDatabaseName("IX_CompensationLogs_ReadyForProcessing");

        builder.HasIndex(x => x.BookingId)
            .HasDatabaseName("IX_CompensationLogs_BookingId");

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_CompensationLogs_IdempotencyKey");

        builder.HasIndex(x => new { x.ProcessingLock, x.ProcessingLockedAt })
            .HasDatabaseName("IX_CompensationLogs_Processing");
    }
}
