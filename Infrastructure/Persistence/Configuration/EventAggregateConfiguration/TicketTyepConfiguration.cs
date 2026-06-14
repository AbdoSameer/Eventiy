using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Configuration.EventAggregateConfiguration
{
    internal class TicketTyepConfiguration : IEntityTypeConfiguration<TicketType>
    {
        public void Configure(EntityTypeBuilder<TicketType> builder)
        {
            builder.ToTable("TicketTypes");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                   .HasConversion(
                    id => id.Value,
                    value => new TicketTypeId(value));

            builder.OwnsOne(x => x.Price, money =>
            {
                money.Property(x => x.Amount)
                     .HasColumnName("Price");

                money.Property(x => x.Currency)
                     .HasColumnName("Currency")
                     .HasMaxLength(3);
            });

            builder.Property(x => x.EventId)
                   .HasConversion(
                       id => id.Value,
                       value => new EventId(value));

            builder.Property(T=> T.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(T => T.Capacity)
                .IsRequired();
            
        }
    }
}
