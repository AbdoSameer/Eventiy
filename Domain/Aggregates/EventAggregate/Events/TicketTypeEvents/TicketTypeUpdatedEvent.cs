using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public abstract class TicketTypeUpdatedEvent : DomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }

        protected TicketTypeUpdatedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
        }
    }
}