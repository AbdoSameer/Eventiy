using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("Bookings");

            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id)
                   .HasConversion(
                       id => id.Value,
                       value => BookingId.FromDatabase(value))
                   .HasColumnName("Id")
                   .IsRequired();

            // UserId
            builder.Property(b => b.UserId)
                   .HasConversion(
                       id => id.Value,
                       value => UserId.FromDatabase(value))
                   .HasColumnName("UserId")
                   .IsRequired();

            // EventId
            builder.Property(b => b.EventId)
                   .HasConversion(
                       id => id.Value,
                       value => EventId.FromDatabase(value))
                   .HasColumnName("EventId")
                   .IsRequired();

            // TicketTypeId
            builder.Property(b => b.TicketTypeId)
                   .HasConversion(
                       id => id.Value,
                       value => TicketTypeId.FromDatabase(value))
                   .HasColumnName("TicketTypeId")
                   .IsRequired();

            // EventTitle
            builder.Property(b => b.EventTitle)
                   .HasColumnName("EventTitle")
                   .IsRequired()
                   .HasMaxLength(200);

            // Quantity
            builder.Property(b => b.Quantity)
                   .HasColumnName("Quantity")
                   .IsRequired();

            // BookingDate
            builder.Property(b => b.BookingDate)
                   .HasColumnName("BookingDate")
                   .IsRequired()
                   .HasDefaultValueSql("GETUTCDATE()");

            // Status (Enum stored as string)
            builder.Property(b => b.Status)
                   .HasColumnName("Status")
                   .HasConversion<string>()
                   .IsRequired()
                   .HasMaxLength(50);

            // Money (Value Object)
            builder.OwnsOne(b => b.Money, money =>
            {
                money.Property(m => m.Amount)
                     .HasColumnName("Amount")
                     .HasPrecision(18, 2)
                     .IsRequired();

                money.Property(m => m.Currency)
                     .HasColumnName("Currency")
                     .HasMaxLength(3)
                     .IsRequired();
            });

            // TotalAmount
            builder.Property(b => b.TotalAmount)
                   .HasColumnName("TotalAmount")
                   .HasPrecision(18, 2)
                   .IsRequired();

            // ConfirmationDate
            builder.Property(b => b.ConfirmationDate)
                   .HasColumnName("ConfirmationDate")
                   .IsRequired(false);

            // CancellationDate
            builder.Property(b => b.CancellationDate)
                   .HasColumnName("CancellationDate")
                   .IsRequired(false);

            // RefundDate
            builder.Property(b => b.RefundDate)
                   .HasColumnName("RefundDate")
                   .IsRequired(false);

            // CancellationReason
            builder.Property(b => b.CancellationReason)
                   .HasColumnName("CancellationReason")
                   .IsRequired(false)
                   .HasMaxLength(500);

            // PaymentMethod (Enum stored as string)
            builder.Property(b => b.PaymentMethod)
                   .HasColumnName("PaymentMethod")
                   .HasConversion<string>()
                   .IsRequired()
                   .HasMaxLength(20);

            // ReferenceCode (for deferred/Fawry payments)
            builder.Property(b => b.ReferenceCode)
                   .HasColumnName("ReferenceCode")
                   .HasMaxLength(20)
                   .IsRequired(false);

            builder.HasIndex(b => b.ReferenceCode)
                   .HasDatabaseName("IX_Bookings_ReferenceCode")
                   .IsUnique()
                   .HasFilter("[ReferenceCode] IS NOT NULL");

            // ===== Indexes ============
            builder.HasIndex(b => b.UserId)
                   .HasDatabaseName("IX_Bookings_UserId");

            builder.HasIndex(b => b.EventId)
                   .HasDatabaseName("IX_Bookings_EventId");

            builder.HasIndex(b => b.TicketTypeId)
                   .HasDatabaseName("IX_Bookings_TicketTypeId");

            builder.HasIndex(b => b.Status)
                   .HasDatabaseName("IX_Bookings_Status");

            builder.HasIndex(b => b.BookingDate)
                   .HasDatabaseName("IX_Bookings_BookingDate");

            // Composite Indexes
            builder.HasIndex(b => new { b.UserId, b.Status })
                   .HasDatabaseName("IX_Bookings_UserId_Status");

            builder.HasIndex(b => new { b.EventId, b.Status })
                   .HasDatabaseName("IX_Bookings_EventId_Status");

            builder.HasIndex(b => new { b.BookingDate, b.Status })
                   .HasDatabaseName("IX_Bookings_BookingDate_Status");

            // ===== Concurrency ============
            builder.Property(b => b.RowVersion)
                   .IsRowVersion()
                   .HasColumnName("RowVersion");
        }
    }
}