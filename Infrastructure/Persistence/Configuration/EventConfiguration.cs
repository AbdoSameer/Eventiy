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
            builder.ToTable("Events");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                   .HasConversion(
                       id => id.Value,
                       value => EventId.Create(value).Value)
                   .HasColumnName("Id")
                   .IsRequired();

            builder.OwnsOne(x => x.EventName, nameBuilder =>
            {
                nameBuilder.Property(n => n.Value)
                           .HasColumnName("Name")
                           .IsRequired()
                           .HasMaxLength(100);
                nameBuilder.HasIndex(n => n.Value)
                           .HasDatabaseName("IX_Events_Name");
            });

            builder.Property(x => x.Capacity)
                   .HasColumnName("Capacity")
                   .IsRequired();

            builder.Property(x => x.Date)
                   .HasColumnName("Date")
                   .IsRequired();

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

                locationBuilder.Property(l => l.Latitude)
                               .HasColumnName("Latitude")
                               .IsRequired(false);

                locationBuilder.Property(l => l.Longitude)
                               .HasColumnName("Longitude")
                               .IsRequired(false);
            });

            builder.Property(x => x.Type)
                   .HasColumnName("Type")
                   .IsRequired()
                   .HasConversion<int>();

            builder.Property(x => x.Status)
                   .HasColumnName("Status")
                   .IsRequired()
                   .HasConversion<int>();

            builder.Property(x => x.Description)
                   .HasColumnName("Description")
                   .IsRequired(false)
                   .HasMaxLength(500);

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

            builder.Property(x => x.CreatedAt)
                   .HasColumnName("CreatedAt")
                   .IsRequired()
                   .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(x => x.LastModifiedAt)
                   .HasColumnName("LastModifiedAt")
                   .IsRequired(false);

            builder.Property(x => x.IsHighDemand)
                   .HasColumnName("IsHighDemand")
                   .IsRequired()
                   .HasDefaultValue(false);

            builder.Property(x => x.RowVersion)
                    .IsRowVersion()
                    .HasColumnName("RowVersion");


            builder.HasIndex(x => x.Date)
                   .HasDatabaseName("IX_Events_Date");

            builder.HasIndex(x => x.Status)
                   .HasDatabaseName("IX_Events_Status");

            builder.HasIndex(x => new { x.Date, x.Status })
                   .HasDatabaseName("IX_Events_Date_Status");

            builder.HasIndex(x => x.CreatedAt)
                   .HasDatabaseName("IX_Events_CreatedAt");

        }
    }
}