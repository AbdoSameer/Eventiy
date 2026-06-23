using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeAddedEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public TicketTypeId TicketTypeId { get; }
        public string TicketName { get; }
        public decimal Price { get; }
        public int Capacity { get; }
        public override string Name => nameof(TicketTypeAddedEvent);
        public override string Domain => "Event";

        public TicketTypeAddedEvent(
            EventId eventId,
            TicketTypeId ticketTypeId,
            string ticketName,
            decimal price,
            int capacity)
        {
            EventId = eventId;
            TicketTypeId = ticketTypeId;
            TicketName = ticketName;
            Price = price;
            Capacity = capacity;
        }
    }
}