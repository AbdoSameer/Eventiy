using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

internal sealed class OutboxDeadLetterConfiguration : IEntityTypeConfiguration<OutboxDeadLetter>
{
    public void Configure(EntityTypeBuilder<OutboxDeadLetter> builder)
    {
        builder.ToTable("OutboxDeadLetters");

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

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.FailedReason)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.MovedToDeadLetterAt)
            .IsRequired();

        builder.HasIndex(x => x.Domain)
            .HasDatabaseName("IX_OutboxDeadLetters_Domain");
    }
}
