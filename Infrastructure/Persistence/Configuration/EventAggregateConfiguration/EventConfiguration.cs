using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.EventAggregateConfiguration
{
    public class EventConfiguration : IEntityTypeConfiguration<Event>
    {
        public void Configure(EntityTypeBuilder<Event> builder)
        {
            builder.ToTable("Events");

            builder.HasKey(x => x.Id);

            builder.Property(E => E.Id).HasConversion(
                EventId => EventId.Value,
                Value =>  EventId.Create(Value).Value);


        builder.Property(E => E.Description).HasMaxLength(1000).IsRequired();
       
            builder.Property(E=>E.Name).HasMaxLength(100).IsRequired()
                .HasConversion(
                name => name.Value,
                Value =>EventName.Create(Value).Value);


            builder.Property(x => x.Capacity).IsRequired();


            builder.Property(E => E.Date).IsRequired();
            builder.OwnsOne(x => x.Location, address =>
            {
                address.Property(x => x.Country)
                       .HasMaxLength(100)
                       .IsRequired();

                address.Property(x => x.City)
                       .HasMaxLength(100)
                       .IsRequired();

                address.Property(x => x.Street)
                       .HasMaxLength(200)
                       .IsRequired();
            });

            builder.Property(x => x.Status)
                   .HasConversion<string>();

            builder.Navigation(e => e.TicketTypes)
                   .HasField("_ticketTypes")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(e => e.TicketTypes)
                   .WithOne()
                   .HasForeignKey("EventId");




        }
    }
}
