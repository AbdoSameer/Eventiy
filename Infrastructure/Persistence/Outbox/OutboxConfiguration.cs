namespace Infrastructure.Persistence.Configuration;

using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.Property(x => x.OccurredOnUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.IsProcessed, x.ProcessedOnUtc })
            .HasDatabaseName("IX_OutboxMessages_Processed");

        builder.HasIndex(x => new { x.IsProcessed, x.NextRetryOnUtc })
            .HasDatabaseName("IX_OutboxMessages_ReadyForProcessing");

        builder.HasIndex(x => x.Domain)
            .HasDatabaseName("IX_OutboxMessages_Domain");
    }
}