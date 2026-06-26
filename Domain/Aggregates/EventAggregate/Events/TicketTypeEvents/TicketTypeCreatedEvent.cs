using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeCreatedEvent : DomainEvent, IDomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }
        public string TicketName { get; }
        public decimal Price { get; }
        public int Capacity { get; }
        public string Currency { get; }
        public override string Name => nameof(TicketTypeCreatedEvent);
        public override string Domain => "Event";

        public TicketTypeCreatedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            string name,
            decimal price,
            int capacity,
            string currency,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata) : base(dateTimeProvider, metadata)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
            TicketName = name;
            Price = price;
            Capacity = capacity;
            Currency = currency;
        }
    }
}