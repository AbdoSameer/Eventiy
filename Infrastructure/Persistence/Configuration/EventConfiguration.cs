using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration
{
    internal class EventConfiguration : IEntityTypeConfiguration<Event>
    {
        public void Configure(EntityTypeBuilder<Event> builder)
        {
            // Table name
            builder.ToTable("Events");

            // Primary Key
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                   .HasConversion(
                       id => id.Value,
                       value => EventId.Create(value).Value)
                   .HasColumnName("Id")
                   .IsRequired();

            // Name (EventName Value Object)
            builder.OwnsOne(x => x.EventName, nameBuilder =>
            {
                nameBuilder.Property(n => n.Value)
                           .HasColumnName("Name")
                           .IsRequired()
                           .HasMaxLength(100);
            });

            // Capacity
            builder.Property(x => x.Capacity)
                   .HasColumnName("Capacity")
                   .IsRequired();

            // Date
            builder.Property(x => x.Date)
                   .HasColumnName("Date")
                   .IsRequired();

            // Location (Address Value Object)
            builder.OwnsOne(x => x.Location, locationBuilder =>
            {
                locationBuilder.Property(l => l.Country)
                               .HasColumnName("Country")
                               .IsRequired()
                               .HasMaxLength(100);

                locationBuilder.Property(l => l.City)
                               .HasColumnName("City")
                               .IsRequired()
                               .HasMaxLength(100);

                locationBuilder.Property(l => l.Street)
                               .HasColumnName("Street")
                               .IsRequired()
                               .HasMaxLength(200);

                locationBuilder.Property(l => l.PostalCode)
                               .HasColumnName("PostalCode")
                               .IsRequired(false)
                               .HasMaxLength(20);
            });

            // Status - Fixed the default value issue
            builder.Property(x => x.Status)
                   .HasColumnName("Status")
                   .IsRequired()
                   .HasConversion<int>();
            // Removed .HasDefaultValue to avoid design-time errors

            // Description
            builder.Property(x => x.Description)
                   .HasColumnName("Description")
                   .IsRequired(false)
                   .HasMaxLength(500);

            // Tracking fields
            builder.Property(x => x.PublishedAt)
                   .HasColumnName("PublishedAt")
                   .IsRequired(false);

            builder.Property(x => x.CancelledAt)
                   .HasColumnName("CancelledAt")
                   .IsRequired(false);

            builder.Property(x => x.CompletedAt)
                   .HasColumnName("CompletedAt")
                   .IsRequired(false);

            builder.Property(x => x.CancellationReason)
                   .HasColumnName("CancellationReason")
                   .IsRequired(false)
                   .HasMaxLength(500);

            // One-to-Many relationship with TicketTypes
            builder.HasMany(e => e.TicketTypes)
                   .WithOne()
                   .HasForeignKey("EventId")
                   .OnDelete(DeleteBehavior.Cascade);

            // ===== Indexes =============
            builder.HasIndex(x => x.Date)
                   .HasDatabaseName("IX_Events_Date");

            builder.HasIndex(x => x.Status)
                   .HasDatabaseName("IX_Events_Status");

            builder.HasIndex(x => new { x.Date, x.Status })
                   .HasDatabaseName("IX_Events_Date_Status");

            // Index for Name
            builder.OwnsOne(x => x.EventName, nameBuilder =>
            {
                nameBuilder.HasIndex(n => n.Value)
                           .HasDatabaseName("IX_Events_Name");
            });

            // ===== Concurrency ===========
            builder.Property<byte[]>("RowVersion")
                   .IsRowVersion()
                   .HasColumnName("RowVersion");
        }
    }
}