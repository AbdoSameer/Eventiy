using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Infrastructure.Configuration.EventAggregateConfiguration
{
    public class EventConfiguration : IEntityTypeConfiguration<Event>
    {
        public void Configure(EntityTypeBuilder<Event> builder)
        {
            builder.ToTable("Events");

            builder.HasKey(x => x.Id);

            builder.Property(E => E.Id).HasConversion(
                EventId => EventId.Value,
                Value => new EventId(Value));

            
            builder.Property(E => E.Description).HasMaxLength(1000).IsRequired();
       
            builder.Property(E=>E.Name).HasMaxLength(100).IsRequired()
                .HasConversion(
                EventName => EventName.Value,
                Value =>EventName.Create(Value).Value);
         
            builder.Property(x => x.Capacity)
                   .HasConversion(
                       c => c.Capacity,
                       value => EventCapacity.Create(value).Value);


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

            builder.HasMany(typeof(TicketType), "_ticketTypes")
                   .WithOne()
                   .HasForeignKey("EventId");

            builder.Ignore(x => x.TotalSeats);


        }
    }
}
