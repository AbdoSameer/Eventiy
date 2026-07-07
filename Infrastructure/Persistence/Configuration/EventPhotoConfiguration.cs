using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

internal sealed class EventPhotoConfiguration : IEntityTypeConfiguration<EventPhoto>
{
    public void Configure(EntityTypeBuilder<EventPhoto> builder)
    {
        builder.ToTable("EventPhotos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
               .HasConversion(
                   id => id.Value,
                   value => EventPhotoId.FromDatabase(value))
               .HasColumnName("Id")
               .IsRequired();

        builder.Property(p => p.EventId)
               .HasConversion(
                   id => id.Value,
                   value => EventId.FromDatabase(value))
               .HasColumnName("EventId")
               .IsRequired();

        builder.Property(p => p.FileName)
               .HasColumnName("FileName")
               .IsRequired()
               .HasMaxLength(255);

        builder.Property(p => p.StoragePath)
               .HasColumnName("StoragePath")
               .IsRequired()
               .HasMaxLength(1000);

        builder.Property(p => p.PublicUrl)
               .HasColumnName("PublicUrl")
               .IsRequired()
               .HasMaxLength(1000);

        builder.Property(p => p.Caption)
               .HasColumnName("Caption")
               .IsRequired(false)
               .HasMaxLength(500);

        builder.Property(p => p.DisplayOrder)
               .HasColumnName("DisplayOrder")
               .HasDefaultValue(0);

        builder.Property(p => p.IsCover)
               .HasColumnName("IsCover")
               .HasDefaultValue(false);

        builder.Property(p => p.UploadedAt)
               .HasColumnName("UploadedAt")
               .IsRequired()
               .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne<Event>()
               .WithMany(e => e.Photos)
               .HasForeignKey("EventId")
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.EventId, p.IsCover })
               .HasDatabaseName("IX_EventPhotos_EventId_IsCover");

        builder.HasIndex(p => p.DisplayOrder)
               .HasDatabaseName("IX_EventPhotos_DisplayOrder");
    }
}
