using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration
{
    internal class TicketTypeConfiguration : IEntityTypeConfiguration<TicketType>
    {
        public void Configure(EntityTypeBuilder<TicketType> builder)
        {
            // Table name
            builder.ToTable("TicketTypes");

            // Primary Key
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                   .HasConversion(
                       id => id.Value,
                       value => TicketTypeId.Create(value).Value)
                   .HasColumnName("Id")
                   .IsRequired();

            // EventId - Foreign Key
            builder.Property(x => x.EventId)
                   .HasConversion(
                       id => id.Value,
                       value => EventId.Create(value).Value)
                   .HasColumnName("EventId")
                   .IsRequired();

            // Name
            builder.Property(x => x.TicketTypeName)
                   .HasColumnName("Name")
                   .IsRequired()
                   .HasMaxLength(100);

            // Price (Money Value Object)
            builder.OwnsOne(x => x.Price, priceBuilder =>
            {
                priceBuilder.Property(p => p.Amount)
                            .HasColumnName("Price")
                            .HasPrecision(18, 2)
                            .IsRequired();

                priceBuilder.Property(p => p.Currency)
                            .HasColumnName("Currency")
                            .HasMaxLength(3)
                            .IsRequired();

                priceBuilder.WithOwner();
            });

            // Capacity
            builder.Property(x => x.Capacity)
                   .HasColumnName("Capacity")
                   .IsRequired();

            // SoldCount
            builder.Property(x => x.SoldCount)
                   .HasColumnName("SoldCount")
                   .IsRequired()
                   .HasDefaultValue(0);

            // ✅ RESERVED COUNT - Added (was missing!)
            builder.Property(x => x.ReservedCount)
                   .HasColumnName("ReservedCount")
                   .IsRequired()
                   .HasDefaultValue(0);

            // CreatedAt
            builder.Property(x => x.CreatedAt)
                   .HasColumnName("CreatedAt")
                   .IsRequired()
                   .HasDefaultValueSql("GETUTCDATE()");

            // LastModifiedAt
            builder.Property(x => x.LastModifiedAt)
                   .HasColumnName("LastModifiedAt")
                   .IsRequired(false);

            // ===== Computed Properties - Must be IGNORED (not stored in DB) =====

            // Original ignored properties
            builder.Ignore(x => x.AvailableCount);

            builder.Ignore(x => x.OccupancyRate);
            builder.Ignore(x => x.ReservationRate);
            builder.Ignore(x => x.UnavailableCount);


            // ===== Indexes =====

            // Existing indexes
            builder.HasIndex(x => x.EventId)
                   .HasDatabaseName("IX_TicketTypes_EventId");

            builder.HasIndex(x => x.TicketTypeName)
                   .HasDatabaseName("IX_TicketTypes_Name");

            // Unique constraint: EventId + Name
            builder.HasIndex(x => new { x.EventId, x.TicketTypeName })
                   .IsUnique()
                   .HasDatabaseName("UX_TicketTypes_EventId_Name");

            builder.HasIndex(x => x.Capacity)
                   .HasDatabaseName("IX_TicketTypes_Capacity");

            builder.HasIndex(x => x.SoldCount)
                   .HasDatabaseName("IX_TicketTypes_SoldCount");

            builder.HasIndex(x => new { x.EventId, x.Capacity, x.SoldCount })
                   .HasDatabaseName("IX_TicketTypes_EventId_Capacity_SoldCount");

            // ✅ NEW: Index for reservation queries (flash-sale scenarios)
            builder.HasIndex(x => new { x.EventId, x.ReservedCount })
                   .HasDatabaseName("IX_TicketTypes_EventId_ReservedCount");

            // ✅ NEW: Composite index for availability checks (most important for seat reservation)
            builder.HasIndex(x => new { x.EventId, x.Capacity, x.SoldCount, x.ReservedCount })
                   .HasDatabaseName("IX_TicketTypes_EventId_Capacity_SoldCount_ReservedCount");

            // ===== Relationships =====

            // Many-to-One with Event
            builder.HasOne<Event>()
                   .WithMany(e => e.TicketTypes)
                   .HasForeignKey(x => x.EventId)
                   .OnDelete(DeleteBehavior.Cascade)
                   .HasConstraintName("FK_TicketTypes_Events_EventId");

            // ===== Concurrency =====
            builder.Property(x => x.RowVersion)
                   .IsRowVersion()
                   .HasColumnName("RowVersion");
        }
    }
}