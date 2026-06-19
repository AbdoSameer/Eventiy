using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.BookingAggregateConfiguration
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("Bookings");

            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id).HasConversion
                (BookId => BookId.Value,
                   value => BookingId.FromDatabase(value));

            builder.Property(b => b.EventId).HasConversion
                (Eventid => Eventid.Value,
                   value => EventId.FromDatabase(value));

            builder.Property(b => b.UserId).HasConversion
                (userid => userid.Value,
                   value => UserId.FromDatabase(value));

            builder.OwnsOne(x => x.Money, money =>
            {
                money.Property(x => x.Amount)
                     .HasColumnName("Amount");

                money.Property(x => x.Currency)
                     .HasColumnName("Currency");
            });

            builder.Property(b => b.Status)
                   .HasConversion<string>()
                   .IsRequired();

            builder.Property(b => b.Quantity).IsRequired();
            builder.Property(b => b.TotalAmount).IsRequired();
            builder.Property(b => b.BookingDate).IsRequired();



        }
    }
}
