using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

internal sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvents");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.IdempotencyKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .IsRequired();

        builder.HasIndex(e => e.IdempotencyKey)
            .IsUnique();
    }
}
